using Farmacia.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.BaseUnitName).HasMaxLength(50);
        builder.Property(p => p.ActiveIngredient).HasMaxLength(200);
        builder.Property(p => p.Concentration).HasMaxLength(100);
        builder.Property(p => p.PharmaceuticalForm).HasMaxLength(100);
        builder.Property(p => p.SanitaryRegistration).HasMaxLength(50);

        builder.HasIndex(p => new { p.PharmacyId, p.Name });

        // Category is another aggregate: FK only, no navigation.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // SaleUnits belong to the Product aggregate: they live and die with it.
        builder.HasMany(p => p.SaleUnits)
            .WithOne()
            .HasForeignKey(u => u.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.SaleUnits).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class SaleUnitConfiguration : IEntityTypeConfiguration<SaleUnit>
{
    public void Configure(EntityTypeBuilder<SaleUnit> builder)
    {
        builder.ToTable("SaleUnits");

        builder.Property(u => u.Name).HasMaxLength(50);
        builder.Property(u => u.Price).HasPrecision(18, 2);
        builder.Property(u => u.Barcode).HasMaxLength(50);

        builder.HasIndex(u => new { u.ProductId, u.Name }).IsUnique();
        builder.HasIndex(u => new { u.PharmacyId, u.Barcode }).IsUnique().HasFilter("[Barcode] IS NOT NULL");
    }
}
