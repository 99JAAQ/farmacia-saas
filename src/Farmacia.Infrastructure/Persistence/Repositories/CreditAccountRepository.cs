using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Credit;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CreditAccountRepository(FarmaciaDbContext db) : ICreditAccountRepository
{
    public Task<CreditAccount?> GetByCustomerAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        db.CreditAccounts.SingleOrDefaultAsync(a => a.PharmacyId == pharmacyId && a.CustomerId == customerId, cancellationToken);

    public async Task<IReadOnlyList<CreditAccount>> GetByCustomersAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken) =>
        await db.CreditAccounts
            .AsNoTracking()
            .Where(a => a.PharmacyId == pharmacyId && customerIds.Contains(a.CustomerId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreditAccount>> ListDebtorsAsync(Guid pharmacyId, int take, CancellationToken cancellationToken) =>
        await db.CreditAccounts
            .AsNoTracking()
            .Where(a => a.PharmacyId == pharmacyId && a.Balance > 0)
            .OrderBy(a => a.DebtSince)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreditEntry>> ListEntriesAsync(
        Guid pharmacyId, Guid creditAccountId, int take, CancellationToken cancellationToken) =>
        await db.CreditEntries
            .AsNoTracking()
            .Where(e => e.PharmacyId == pharmacyId && e.CreditAccountId == creditAccountId)
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id) // Guid v7: two entries at the same instant keep their order
            .Take(take)
            .ToListAsync(cancellationToken);

    // FarmaciaDbContext.SaveChanges adds the account's pending entries in the same transaction.
    public void Add(CreditAccount account) => db.CreditAccounts.Add(account);
}
