using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Credit;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CreditAccountRepository(FarmaciaDbContext db) : ICreditAccountRepository
{
    public Task<CreditAccount?> GetByCustomerAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        db.CreditAccounts.SingleOrDefaultAsync(a => a.PharmacyId == pharmacyId && a.CustomerId == customerId, cancellationToken);

    // FarmaciaDbContext.SaveChanges adds the account's pending entries in the same transaction.
    public void Add(CreditAccount account) => db.CreditAccounts.Add(account);
}
