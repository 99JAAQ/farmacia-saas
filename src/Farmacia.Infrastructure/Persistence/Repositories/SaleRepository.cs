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

    public void Add(Sale sale) => db.Sales.Add(sale);
}
