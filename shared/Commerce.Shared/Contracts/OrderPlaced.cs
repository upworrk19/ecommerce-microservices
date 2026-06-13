namespace Commerce.Shared.Contracts;

/// <summary>
/// Published by Order, consumed by Notification. Single source of truth for the
/// event schema — both publisher and consumer reference THIS type, so the wire
/// contract cannot drift.
/// </summary>
public record OrderPlaced
{
    public required Guid OrderId { get; init; }
    public required string UserId { get; init; }
    public required string UserEmail { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal TotalPrice { get; init; }
    public DateTimeOffset PlacedAt { get; init; } = DateTimeOffset.UtcNow;
}
