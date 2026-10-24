using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.Property(p => p.InvoiceNumber).HasMaxLength(50);
        builder.Property(p => p.InvoiceDiscount).HasPrecision(18, 2);

        // Optimistic concurrency: a double click on "Confirmar" (two requests at once) must not create the
        // batches twice. The second SaveChanges finds a different RowVersion and fails (409).
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // The same supplier invoice cannot be typed in twice.
        builder.HasIndex(p => new { p.PharmacyId, p.SupplierId, p.InvoiceNumber }).IsUnique();
        builder.HasIndex(p => new { p.PharmacyId, p.Status, p.InvoiceDate });

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Items belong to the aggregate: they live and die with the purchase.
        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        // No DbSet (items are reached through the purchase), so EF would name the table after the class.
        builder.ToTable("PurchaseItems");

        builder.Property(i => i.UnitCost).HasPrecision(18, 2);
        builder.Property(i => i.LineDiscount).HasPrecision(18, 2);
        builder.Property(i => i.UnitCostPerBaseUnit).HasPrecision(18, 4);
        builder.Property(i => i.LotNumber).HasMaxLength(50);

        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaleUnit>().WithMany().HasForeignKey(i => i.SaleUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Batch>().WithMany().HasForeignKey(i => i.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
