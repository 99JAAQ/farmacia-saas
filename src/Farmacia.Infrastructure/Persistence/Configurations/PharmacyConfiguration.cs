using Farmacia.Domain.Pharmacies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class PharmacyConfiguration : IEntityTypeConfiguration<Pharmacy>
{
    public void Configure(EntityTypeBuilder<Pharmacy> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Address).HasMaxLength(300);
        builder.Property(p => p.Phone).HasMaxLength(30);

        builder.HasIndex(p => p.TaxId).IsUnique();
    }
}
