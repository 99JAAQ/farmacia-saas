using Farmacia.Domain.Cash;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;
using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farmacia.Infrastructure.Persistence.Configurations;

public class CreditAccountConfiguration : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.Property(a => a.Limit).HasPrecision(18, 2);
        builder.Property(a => a.Balance).HasPrecision(18, 2);

        // Saved by FarmaciaDbContext.SaveChanges as CreditEntry rows, like a batch's kardex.
        builder.Ignore(a => a.PendingEntries);

        // Optimistic concurrency: two registers charging the same customer at once cannot both pass the limit.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // One account per customer.
        builder.HasIndex(a => new { a.PharmacyId, a.CustomerId }).IsUnique();
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);

        // The owner's list of who owes.
        builder.HasIndex(a => new { a.PharmacyId, a.Balance });
    }
}

public class CreditEntryConfiguration : IEntityTypeConfiguration<CreditEntry>
{
    public void Configure(EntityTypeBuilder<CreditEntry> builder)
    {
        builder.Property(e => e.Change).HasPrecision(18, 2);
        builder.Property(e => e.BalanceAfter).HasPrecision(18, 2);

        builder.HasOne<CreditAccount>().WithMany().HasForeignKey(e => e.CreditAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);

        // The customer's statement, in order.
        builder.HasIndex(e => new { e.CreditAccountId, e.OccurredAt });
    }
}

public class CreditPaymentConfiguration : IEntityTypeConfiguration<CreditPayment>
{
    public void Configure(EntityTypeBuilder<CreditPayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.BalanceAfter).HasPrecision(18, 2);

        // The counter already guarantees it; the index is the last line of defense against a repeated number.
        builder.HasIndex(p => new { p.PharmacyId, p.Number }).IsUnique();

        // A shift's payments (its cash when closing).
        builder.HasIndex(p => new { p.PharmacyId, p.CashSessionId });

        builder.HasOne<CreditAccount>().WithMany().HasForeignKey(p => p.CreditAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(p => p.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.ReceivedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
