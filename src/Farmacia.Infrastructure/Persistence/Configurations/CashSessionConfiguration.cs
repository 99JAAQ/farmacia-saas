using Farmacia.Domain.Cash;
using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    /// <summary>Its name is matched by <see cref="UnitOfWork"/> to give a specific message.</summary>
    public const string OneOpenPerPharmacyIndex = "IX_CashSessions_OneOpenPerPharmacy";

    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.Property(s => s.OpeningFloat).HasPrecision(18, 2);
        builder.Property(s => s.ExpectedCash).HasPrecision(18, 2);
        builder.Property(s => s.CountedCash).HasPrecision(18, 2);
        builder.Property(s => s.ClosingNotes).HasMaxLength(500);

        // Optimistic concurrency: two closings at the same time (a double click) cannot both win.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // One register: at most one open shift per pharmacy. Filtered, so closed shifts do not count.
        // Two simultaneous "open" clicks both pass the use case's check; this index rejects the second one.
        builder.HasIndex(s => s.PharmacyId)
            .IsUnique()
            .HasFilter($"[Status] = {(int)CashSessionStatus.Open}")
            .HasDatabaseName(OneOpenPerPharmacyIndex);

        builder.HasIndex(s => new { s.PharmacyId, s.Status, s.ClosedAt });

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OpenedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.ClosedBy).OnDelete(DeleteBehavior.Restrict);

        // Movements belong to the aggregate: they live and die with the shift.
        builder.HasMany(s => s.Movements)
            .WithOne()
            .HasForeignKey(m => m.CashSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        // No DbSet (movements are reached through the shift), so EF would name the table after the class.
        builder.ToTable("CashMovements");

        builder.Property(m => m.Amount).HasPrecision(18, 2);
        builder.Property(m => m.Reason).HasMaxLength(200);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.RegisteredBy).OnDelete(DeleteBehavior.Restrict);
    }
}
