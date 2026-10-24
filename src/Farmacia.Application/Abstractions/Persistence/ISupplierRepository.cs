using Farmacia.Domain.Common;
using Farmacia.Domain.Suppliers;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ISupplierRepository
{
    Task<bool> ExistsAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken);

    Task<Supplier?> GetByIdAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken);

    /// <summary>By name or NIT, ignoring case and accents, alphabetically. Empty search: the first ones.</summary>
    Task<IReadOnlyList<Supplier>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken);

    Task<bool> TaxIdExistsAsync(Guid pharmacyId, Nit taxId, CancellationToken cancellationToken);

    void Add(Supplier supplier);
}
