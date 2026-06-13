using System.Text.Json;
using StackExchange.Redis;

namespace Commerce.Notification.Data;

public record NotificationEntry(Guid OrderId, string Message, DateTimeOffset CreatedAt);

public class NotificationStore(IConnectionMultiplexer redis)
{
    private IDatabase Db => redis.GetDatabase();
    private static string Key(string userId) => $"notifications:{userId}";

    public async Task AddAsync(string userId, NotificationEntry n) =>
        await Db.ListRightPushAsync(Key(userId), JsonSerializer.Serialize(n));

    public async Task<IReadOnlyList<NotificationEntry>> GetAsync(string userId)
    {
        var items = await Db.ListRangeAsync(Key(userId));
        return items
            .Select(v => (string?)v)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => JsonSerializer.Deserialize<NotificationEntry>(s!))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    /// <summary>
    /// Returns true only the FIRST time a given order is processed. Uses Redis
    /// SET NX so redelivered OrderPlaced events (at-least-once delivery) are
    /// ignored — the idempotent-consumer counterpart to the publisher's outbox.
    /// </summary>
    public async Task<bool> TryMarkProcessedAsync(Guid orderId) =>
        await Db.StringSetAsync($"notifications:processed:{orderId}", "1",
            expiry: TimeSpan.FromDays(7), when: When.NotExists);
}
