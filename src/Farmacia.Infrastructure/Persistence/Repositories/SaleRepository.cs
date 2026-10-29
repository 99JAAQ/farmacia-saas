using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class SaleRepository(FarmaciaDbContext db) : ISaleRepository
{
    public Task<Sale?> GetByIdAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken) =>
        db.Sales
            .Include(s => s.Items).ThenInclude(i => i.Batches)
            .Include(s => s.Payments)
            .AsSplitQuery() // avoids repeating the sale row once per item, batch and payment
            .SingleOrDefaultAsync(s => s.PharmacyId == pharmacyId && s.Id == saleId, cancellationToken);

    public Task<Sale?> GetByNumberAsync(Guid pharmacyId, long number, CancellationToken cancellationToken) =>
        db.Sales
            .Include(s => s.Items).ThenInclude(i => i.Batches)
            .Include(s => s.Payments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.PharmacyId == pharmacyId && s.Number == number, cancellationToken);

    public async Task<IReadOnlyList<Sale>> ListByCashSessionAsync(
        Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        await db.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Where(s => s.PharmacyId == pharmacyId && s.CashSessionId == cashSessionId)
            .OrderByDescending(s => s.Number)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public void Add(Sale sale) => db.Sales.Add(sale);
}
