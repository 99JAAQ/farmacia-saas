using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class SupplierRepository(FarmaciaDbContext db) : ISupplierRepository
{
    public Task<bool> ExistsAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken) =>
        db.Suppliers.AnyAsync(s => s.PharmacyId == pharmacyId && s.Id == supplierId, cancellationToken);

    public async Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken) =>
        await db.Suppliers
            .Where(s => s.PharmacyId == pharmacyId && supplierIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
}
