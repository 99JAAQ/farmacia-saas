using Farmacia.Domain.Cash;
using Farmacia.Domain.Customers;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;
using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        // The counter already guarantees it; the index is the last line of defense against a repeated ticket.
        builder.HasIndex(s => new { s.PharmacyId, s.Number }).IsUnique();

        // The shift's sales: its cash when closing, the list of tickets.
        builder.HasIndex(s => new { s.PharmacyId, s.CashSessionId });
        builder.HasIndex(s => new { s.PharmacyId, s.SoldAt });

        // A customer's purchases (credit statement, history).
        builder.HasIndex(s => new { s.PharmacyId, s.CustomerId });
        builder.HasOne<Customer>().WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CashSession>().WithMany().HasForeignKey(s => s.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.SoldBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.PrescriptionCheckedBy).OnDelete(DeleteBehavior.Restrict);

        // Items and payments belong to the aggregate. A sale is never deleted (it is voided or refunded), but if it
        // were, its lines would go with it.
        builder.HasMany(s => s.Items).WithOne().HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(s => s.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        // No DbSet (items are reached through the sale), so EF would name the table after the class.
        builder.ToTable("SaleItems");

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaleUnit>().WithMany().HasForeignKey(i => i.SaleUnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Batches).WithOne().HasForeignKey(b => b.SaleItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Batches).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class SaleItemBatchConfiguration : IEntityTypeConfiguration<SaleItemBatch>
{
    public void Configure(EntityTypeBuilder<SaleItemBatch> builder)
    {
        builder.ToTable("SaleItemBatches");

        // Per base unit, like Batch.UnitCost.
        builder.Property(b => b.UnitCost).HasPrecision(18, 4);

        builder.HasOne<Batch>().WithMany().HasForeignKey(b => b.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Received).HasPrecision(18, 2);
    }
}

public class DocumentCounterConfiguration : IEntityTypeConfiguration<DocumentCounter>
{
    public void Configure(EntityTypeBuilder<DocumentCounter> builder)
    {
        builder.ToTable("DocumentCounters");
        builder.HasKey(c => new { c.PharmacyId, c.Document });
        builder.HasOne<Pharmacy>().WithMany().HasForeignKey(c => c.PharmacyId).OnDelete(DeleteBehavior.Restrict);
    }
}
