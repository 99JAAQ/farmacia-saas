using Farmacia.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.ContactName).HasMaxLength(200);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(254);

        builder.HasIndex(s => new { s.PharmacyId, s.TaxId }).IsUnique();
    }
}
