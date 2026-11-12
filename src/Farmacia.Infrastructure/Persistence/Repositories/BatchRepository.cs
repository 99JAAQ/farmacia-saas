using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class BatchRepository(FarmaciaDbContext db) : IBatchRepository
{
    public async Task<IReadOnlyList<Batch>> GetInStockAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        await db.Batches
            .Where(b => b.PharmacyId == pharmacyId && productIds.Contains(b.ProductId) && b.Quantity > 0)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Batch>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken) =>
        await db.Batches
            .Where(b => b.PharmacyId == pharmacyId && batchIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductStock>> GetStockAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, DateOnly today, CancellationToken cancellationToken) =>
        await db.Batches
            .Where(b => b.PharmacyId == pharmacyId && productIds.Contains(b.ProductId) && b.Quantity > 0)
            .GroupBy(b => b.ProductId)
            .Select(g => new ProductStock(
                g.Key,
                // A batch can be sold through its expiration date (Batch.IsExpiredOn): expired from the next day on.
                g.Where(b => b.ExpirationDate >= today).Sum(b => b.Quantity),
                g.Where(b => b.ExpirationDate < today).Sum(b => b.Quantity),
                g.Where(b => b.ExpirationDate >= today).Min(b => (DateOnly?)b.ExpirationDate)))
            .ToListAsync(cancellationToken);

    public Task<Batch?> GetByIdAsync(Guid pharmacyId, Guid batchId, CancellationToken cancellationToken) =>
        db.Batches.SingleOrDefaultAsync(b => b.PharmacyId == pharmacyId && b.Id == batchId, cancellationToken);

    public async Task<IReadOnlyList<Batch>> ListInStockByProductAsync(
        Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        await db.Batches
            .AsNoTracking()
            .Where(b => b.PharmacyId == pharmacyId && b.ProductId == productId && b.Quantity > 0)
            .OrderBy(b => b.ExpirationDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Batch>> ListInStockExpiringByAsync(
        Guid pharmacyId, DateOnly date, CancellationToken cancellationToken) =>
        await db.Batches
            .AsNoTracking()
            .Where(b => b.PharmacyId == pharmacyId && b.Quantity > 0 && b.ExpirationDate <= date)
            .OrderBy(b => b.ExpirationDate)
            .ToListAsync(cancellationToken);

    // FarmaciaDbContext.SaveChanges adds the batch's pending kardex movements in the same transaction.
    public void Add(Batch batch) => db.Batches.Add(batch);
}
