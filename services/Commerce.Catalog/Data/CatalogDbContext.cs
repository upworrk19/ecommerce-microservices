using Microsoft.EntityFrameworkCore;

namespace Commerce.Catalog.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ProductEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Price).HasColumnType("numeric(18,2)");
        });

        b.Entity<ProductEntity>().HasData(
            new ProductEntity { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Mechanical Keyboard", Description = "Hot-swap, 75%", Price = 129.00m, Stock = 50 },
            new ProductEntity { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "USB-C Hub", Description = "7-in-1", Price = 49.50m, Stock = 200 },
            new ProductEntity { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "4K Monitor", Description = "27-inch IPS", Price = 329.99m, Stock = 30 });
    }
}
