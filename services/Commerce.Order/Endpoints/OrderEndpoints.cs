using System.Security.Claims;
using Commerce.Order.Clients;
using Commerce.Order.Data;
using Commerce.Shared.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Order.Endpoints;

public record PlaceOrderRequest(Guid ProductId, int Quantity);

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/orders").RequireAuthorization();

        group.MapPost("/", async (
            PlaceOrderRequest req, HttpContext ctx, OrderDbContext db,
            CatalogClient catalog, IPublishEndpoint publish, CancellationToken ct) =>
        {
            const int MaxQuantity = 1000;
            if (req.Quantity <= 0 || req.Quantity > MaxQuantity)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["quantity"] = [$"Quantity must be between 1 and {MaxQuantity}."]
                });

            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var email = ctx.User.FindFirstValue(ClaimTypes.Email) ?? "unknown@example.com";
            var authHeader = ctx.Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();
            var token = authHeader["Bearer ".Length..].Trim();

            var product = await catalog.GetProductAsync(req.ProductId, token, ct);
            if (product is null) return Results.NotFound(new { title = "Product not found" });

            var order = new OrderEntity
            {
                UserId = userId, UserEmail = email,
                ProductId = product.Id, ProductName = product.Name,
                Quantity = req.Quantity, TotalPrice = product.Price * req.Quantity
            };
            db.Orders.Add(order);

            // Published THROUGH the EF outbox: persisted in the same SaveChanges
            // transaction as the order, then dispatched to RabbitMQ by the outbox.
            await publish.Publish(new OrderPlaced
            {
                OrderId = order.Id, UserId = order.UserId, UserEmail = order.UserEmail,
                ProductId = order.ProductId, ProductName = order.ProductName,
                Quantity = order.Quantity, TotalPrice = order.TotalPrice
            }, ct);

            await db.SaveChangesAsync(ct);
            return Results.Created($"/orders/{order.Id}", new { order.Id, order.TotalPrice });
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, OrderDbContext db, CancellationToken ct) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });
    }
}
