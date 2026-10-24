using Farmacia.Domain.Suppliers;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ISupplierRepository
{
    Task<bool> ExistsAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken);
}
