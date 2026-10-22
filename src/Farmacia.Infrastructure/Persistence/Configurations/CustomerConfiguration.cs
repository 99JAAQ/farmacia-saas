using Farmacia.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.FullName).HasMaxLength(200);
        builder.Property(c => c.DocumentNumber).HasMaxLength(30);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(254);

        builder.HasIndex(c => new { c.PharmacyId, c.DocumentType, c.DocumentNumber }).IsUnique();
    }
}
