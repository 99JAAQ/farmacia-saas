using Farmacia.Domain.Cash;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;
using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        // The counter already guarantees it; the index is the last line of defense against a repeated number.
        builder.HasIndex(r => new { r.PharmacyId, r.Number }).IsUnique();

        // A ticket's returns (what is left to return) and a shift's refunds (its cash when closing).
        builder.HasIndex(r => new { r.PharmacyId, r.SaleId });
        builder.HasIndex(r => new { r.PharmacyId, r.CashSessionId });

        builder.Property(r => r.Reason).HasMaxLength(200);

        builder.HasOne<Sale>().WithMany().HasForeignKey(r => r.SaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(r => r.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReturnedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items).WithOne().HasForeignKey(i => i.SaleReturnId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class SaleReturnItemConfiguration : IEntityTypeConfiguration<SaleReturnItem>
{
    public void Configure(EntityTypeBuilder<SaleReturnItem> builder)
    {
        builder.ToTable("SaleReturnItems");

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        // Restrict: a ticket line that was returned can never disappear from under its return.
        builder.HasOne<SaleItem>().WithMany().HasForeignKey(i => i.SaleItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaleUnit>().WithMany().HasForeignKey(i => i.SaleUnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Batches).WithOne().HasForeignKey(b => b.SaleReturnItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Batches).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class SaleReturnItemBatchConfiguration : IEntityTypeConfiguration<SaleReturnItemBatch>
{
    public void Configure(EntityTypeBuilder<SaleReturnItemBatch> builder)
    {
        builder.ToTable("SaleReturnItemBatches");

        // Per base unit, like Batch.UnitCost.
        builder.Property(b => b.UnitCost).HasPrecision(18, 4);

        builder.HasOne<Batch>().WithMany().HasForeignKey(b => b.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
