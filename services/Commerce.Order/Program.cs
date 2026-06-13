using Commerce.Order.Clients;
using Commerce.Order.Data;
using Commerce.Order.Endpoints;
using Commerce.Shared.Auth;
using Commerce.Shared.Errors;
using Commerce.Shared.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceObservability("order");
builder.UseCommerceSerilog("order");
builder.AddCommerceProblemDetails();
builder.AddCommerceJwtAuth();

builder.Services.AddDbContext<OrderDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<CatalogClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]!))
    .AddStandardResilienceHandler();  // Polly-based retries/circuit-breaker/timeout

builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
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
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHealthChecks().AddDbContextCheck<OrderDbContext>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapOrderEndpoints();
app.MapHealthChecks("/health");

// Apply migrations / ensure DB at startup (demo-grade; see README for prod note).
using (var scope = app.Services.CreateScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Serilog.Log.Fatal(ex, "Database migration failed on startup");
        throw;
    }
}

app.Run();

// Namespaced marker so the integration-test project (which references all 4
// service assemblies) can disambiguate WebApplicationFactory<T> by assembly.
namespace Commerce.Order
{
    public partial class Program;
}
