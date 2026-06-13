using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Order.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<OrderEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalPrice).HasColumnType("numeric(18,2)");
        });

        // MassTransit transactional outbox tables — order row + outbox message
        // commit in ONE transaction so the event can't be lost or duplicated.
        b.AddInboxStateEntity();
        b.AddOutboxMessageEntity();
        b.AddOutboxStateEntity();
    }
}
