using System.Security.Claims;
using Commerce.Notification.Data;

namespace Commerce.Notification.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/notifications/me", async (HttpContext ctx, NotificationStore store) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            return Results.Ok(await store.GetAsync(userId));
        }).RequireAuthorization();
    }
}
