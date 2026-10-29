using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class SaleReturnRepository(FarmaciaDbContext db) : ISaleReturnRepository
{
    public Task<SaleReturn?> GetByIdAsync(Guid pharmacyId, Guid saleReturnId, CancellationToken cancellationToken) =>
        db.SaleReturns
            .Include(r => r.Items).ThenInclude(i => i.Batches)
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.PharmacyId == pharmacyId && r.Id == saleReturnId, cancellationToken);

    public async Task<IReadOnlyList<SaleReturn>> ListBySaleAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken) =>
        await db.SaleReturns
            .Include(r => r.Items).ThenInclude(i => i.Batches)
            .Where(r => r.PharmacyId == pharmacyId && r.SaleId == saleId)
            .OrderBy(r => r.Number)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public void Add(SaleReturn saleReturn) => db.SaleReturns.Add(saleReturn);
}
