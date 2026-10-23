using Farmacia.Domain.Pharmacies;
using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);

        builder.HasOne<Pharmacy>()
            .WithMany()
            .HasForeignKey(u => u.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.PharmacyId);
    }
}
