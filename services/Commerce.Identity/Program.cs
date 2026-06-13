using Commerce.Identity.Auth;
using Commerce.Identity.Data;
using Commerce.Identity.Endpoints;
using Commerce.Shared.Errors;
using Commerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceObservability("identity");
builder.UseCommerceSerilog("identity");
builder.AddCommerceProblemDetails();

builder.Services.AddDbContext<IdentityDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapAuthEndpoints();
app.MapHealthChecks("/health");

// Apply migrations / ensure DB at startup (demo-grade; see README for prod note).
using (var scope = app.Services.CreateScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
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
// The app still runs via the compiler-generated global Program entry point.
namespace Commerce.Identity
{
    public partial class Program;
}
