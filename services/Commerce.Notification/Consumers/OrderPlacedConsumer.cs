using Commerce.Notification.Data;
using Commerce.Shared.Contracts;
using MassTransit;

namespace Commerce.Notification.Consumers;

public class OrderPlacedConsumer(NotificationStore store, ILogger<OrderPlacedConsumer> logger)
    : IConsumer<OrderPlaced>
{
    public async Task Consume(ConsumeContext<OrderPlaced> ctx)
    {
        var e = ctx.Message;
        if (!await store.TryMarkProcessedAsync(e.OrderId))
        {
            logger.LogInformation("Duplicate OrderPlaced for order {OrderId} ignored", e.OrderId);
            return;
        }
        var msg = $"Order {e.OrderId} confirmed: {e.Quantity}x {e.ProductName} (${e.TotalPrice}).";
        await store.AddAsync(e.UserId, new NotificationEntry(e.OrderId, msg, DateTimeOffset.UtcNow));
        logger.LogInformation("Simulated email sent to {Email} for order {OrderId}", e.UserEmail, e.OrderId);
    }
}
