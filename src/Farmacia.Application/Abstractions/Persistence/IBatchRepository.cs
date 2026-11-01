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

    /// <summary>
    /// Per product: units that can be sold (batches not expired on <paramref name="today"/>), units in expired
    /// batches (to write off), and the nearest expiration among the sellable ones. Products without stock are left out.
    /// </summary>
    Task<IReadOnlyList<ProductStock>> GetStockAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Tracked: writing it off or adjusting it is saved, with its kardex entry, by the next SaveChanges.</summary>
    Task<Batch?> GetByIdAsync(Guid pharmacyId, Guid batchId, CancellationToken cancellationToken);

    /// <summary>A product's batches that still have units, the first to expire first. Read-only.</summary>
    Task<IReadOnlyList<Batch>> ListInStockByProductAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken);

    /// <summary>Its pending kardex movements are saved with it, in the same transaction.</summary>
    void Add(Batch batch);
}

/// <summary>A product's stock in base units (e.g. tablets).</summary>
public sealed record ProductStock(Guid ProductId, int SellableUnits, int ExpiredUnits, DateOnly? NextExpiration);
