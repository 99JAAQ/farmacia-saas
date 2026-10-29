using Farmacia.Domain.Inventory;

namespace Farmacia.Application.Abstractions.Persistence;

public interface IBatchRepository
{
    /// <summary>
    /// The batches of the given products that still have units, tracked: selling from them is saved, with their
    /// kardex movements, by the next SaveChanges. Expired ones are included; FEFO skips them.
    /// </summary>
    Task<IReadOnlyList<Batch>> GetInStockAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>The given batches, tracked: e.g. those a sale took units from, to put returned units back.</summary>
    Task<IReadOnlyList<Batch>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken);

    /// <summary>Its pending kardex movements are saved with it, in the same transaction.</summary>
    void Add(Batch batch);
}
