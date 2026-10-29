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

    // FarmaciaDbContext.SaveChanges adds the batch's pending kardex movements in the same transaction.
    public void Add(Batch batch) => db.Batches.Add(batch);
}
