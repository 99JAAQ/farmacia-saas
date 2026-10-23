using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.Id).ValueGeneratedNever();

        // SHA-256 as hex: always 64 characters.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(t => t.RevokedReason).HasMaxLength(50);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);

        // Deleting a user ends all of their sessions.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
