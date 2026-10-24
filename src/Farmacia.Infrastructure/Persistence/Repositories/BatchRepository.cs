using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Inventory;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class BatchRepository(FarmaciaDbContext db) : IBatchRepository
{
    // FarmaciaDbContext.SaveChanges adds the batch's pending kardex movements in the same transaction.
    public void Add(Batch batch) => db.Batches.Add(batch);
}
