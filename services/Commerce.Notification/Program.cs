using Commerce.Notification.Consumers;
using Commerce.Notification.Data;
using Commerce.Notification.Endpoints;
using Commerce.Shared.Auth;
using Commerce.Shared.Errors;
using Commerce.Shared.Observability;
using MassTransit;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceObservability("notification");
builder.UseCommerceSerilog("notification");
builder.AddCommerceProblemDetails();
builder.AddCommerceJwtAuth();

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:Connection"] ?? "localhost:6379"));
builder.Services.AddSingleton<NotificationStore>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        var uri = builder.Configuration["RabbitMq:Uri"];
        if (!string.IsNullOrWhiteSpace(uri))
        {
            // Testcontainers: full amqp:// URI carries the mapped host port + creds.
            cfg.Host(new Uri(uri));
        }
        else
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMq:User"] ?? "guest");
                h.Password(builder.Configuration["RabbitMq:Pass"] ?? "guest");
            });
        }
        cfg.ReceiveEndpoint("order-placed", e =>
        {
            // Resiliency: retry transient failures with incremental backoff, then
            // trip the circuit breaker; exhausted messages go to order-placed_error.
            e.UseMessageRetry(r => r.Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
            e.UseCircuitBreaker(cb =>
            {
                cb.TripThreshold = 15;
                cb.ActiveThreshold = 10;
                cb.ResetInterval = TimeSpan.FromMinutes(1);
            });
            e.ConfigureConsumer<OrderPlacedConsumer>(context);
        });
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapNotificationEndpoints();
app.MapHealthChecks("/health");
app.Run();

// Namespaced marker so the integration-test project (which references all 4
// service assemblies) can disambiguate WebApplicationFactory<T> by assembly.
namespace Commerce.Notification
{
    public partial class Program;
}
