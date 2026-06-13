using Commerce.Shared.Auth;
using Commerce.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceObservability("gateway");
builder.UseCommerceSerilog("gateway");
builder.AddCommerceJwtAuth();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapHealthChecks("/health");
app.Run();
