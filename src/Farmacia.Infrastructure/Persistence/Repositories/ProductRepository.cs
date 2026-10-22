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

    public async Task<IReadOnlyList<string>> GetExistingBarcodesAsync(
        Guid pharmacyId, IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken) =>
        await db.Set<SaleUnit>()
            .Where(u => u.PharmacyId == pharmacyId && u.Barcode != null && barcodes.Contains(u.Barcode))
            .Select(u => u.Barcode!)
            .ToListAsync(cancellationToken);

    public void Add(Product product) => db.Products.Add(product);
}
