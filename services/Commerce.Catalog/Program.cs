using Commerce.Catalog.Data;
using Commerce.Catalog.Endpoints;
using Commerce.Shared.Auth;
using Commerce.Shared.Errors;
using Commerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceObservability("catalog");
builder.UseCommerceSerilog("catalog");
builder.AddCommerceProblemDetails();
builder.AddCommerceJwtAuth();

builder.Services.AddDbContext<CatalogDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapProductEndpoints();
app.MapHealthChecks("/health");

// Apply migrations / ensure DB at startup (demo-grade; see README for prod note).
using (var scope = app.Services.CreateScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
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
namespace Commerce.Catalog
{
    public partial class Program;
}
