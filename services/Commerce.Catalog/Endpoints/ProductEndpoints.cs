using Commerce.Catalog.Data;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Catalog.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products").RequireAuthorization();

        group.MapGet("/", async (CatalogDbContext db) =>
            Results.Ok(await db.Products.AsNoTracking().ToListAsync()));

        group.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db) =>
            await db.Products.FindAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());
    }
}
