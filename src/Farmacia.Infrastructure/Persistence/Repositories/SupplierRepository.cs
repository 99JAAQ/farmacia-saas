using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Common;
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

    public Task<Supplier?> GetByIdAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken) =>
        db.Suppliers.SingleOrDefaultAsync(s => s.PharmacyId == pharmacyId && s.Id == supplierId, cancellationToken);

    public async Task<IReadOnlyList<Supplier>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken)
    {
        var query = db.Suppliers.Where(s => s.PharmacyId == pharmacyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = SearchPattern.Contains(search);
            var nit = TryNit(search);
            query = query.Where(s =>
                EF.Functions.Like(EF.Functions.Collate(s.Name, SearchPattern.Collation), pattern)
                || (nit != null && s.TaxId == nit));
        }

        return await query.OrderBy(s => s.Name).Take(take).ToListAsync(cancellationToken);
    }

    public Task<bool> TaxIdExistsAsync(Guid pharmacyId, Nit taxId, CancellationToken cancellationToken) =>
        db.Suppliers.AnyAsync(s => s.PharmacyId == pharmacyId && s.TaxId == taxId, cancellationToken);

    public void Add(Supplier supplier) => db.Suppliers.Add(supplier);

    /// <summary>The NIT is stored as text with its digit: "800987654" and "800987654-4" both find it.</summary>
    private static Nit? TryNit(string search)
    {
        try
        {
            return search.Contains('-') ? Nit.Parse(search) : Nit.FromNumber(search);
        }
        catch (DomainException)
        {
            return null; // not a NIT: search by name only
        }
    }
}
