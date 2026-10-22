using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.Property(b => b.LotNumber).HasMaxLength(50);

        // Per base unit: a 10,000 box of 30 tablets costs 333.3333 each; 2 decimals would accumulate rounding errors.
        builder.Property(b => b.UnitCost).HasPrecision(18, 4);

        // Saved by FarmaciaDbContext.SaveChanges as StockMovement rows, not as a navigation.
        builder.Ignore(b => b.PendingMovements);

        // Optimistic concurrency: two cashiers selling from the same batch cannot silently overwrite each other's stock.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports FEFO: a product's batches ordered by expiration.
        builder.HasIndex(b => new { b.PharmacyId, b.ProductId, b.ExpirationDate });
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(m => m.Notes).HasMaxLength(500);

        builder.HasOne<Batch>()
            .WithMany()
            .HasForeignKey(m => m.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Kardex queries: history of a batch, and of a product.
        builder.HasIndex(m => new { m.BatchId, m.OccurredAt });
        builder.HasIndex(m => new { m.PharmacyId, m.ProductId, m.OccurredAt });
    }
}
