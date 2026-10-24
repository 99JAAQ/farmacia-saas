using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(FarmaciaDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        db.Products
            .Include(p => p.SaleUnits)
            .SingleOrDefaultAsync(p => p.PharmacyId == pharmacyId && p.Id == productId, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        await db.Products
            .Include(p => p.SaleUnits)
            .Where(p => p.PharmacyId == pharmacyId && productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken)
    {
        var query = db.Products.Include(p => p.SaleUnits).Where(p => p.PharmacyId == pharmacyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var barcode = search.Trim();
            var pattern = SearchPattern.Contains(search);
            query = query.Where(p =>
                EF.Functions.Like(EF.Functions.Collate(p.Name, SearchPattern.Collation), pattern)
                || (p.ActiveIngredient != null
                    && EF.Functions.Like(EF.Functions.Collate(p.ActiveIngredient, SearchPattern.Collation), pattern))
                || p.SaleUnits.Any(u => u.Barcode == barcode)); // a scanned box
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetExistingBarcodesAsync(
        Guid pharmacyId, IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken) =>
        await db.Set<SaleUnit>()
            .Where(u => u.PharmacyId == pharmacyId && u.Barcode != null && barcodes.Contains(u.Barcode))
            .Select(u => u.Barcode!)
            .ToListAsync(cancellationToken);

    public void Add(Product product) => db.Products.Add(product);
}
