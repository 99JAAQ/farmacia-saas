using Farmacia.Domain.Products;

namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>Every read is scoped to a pharmacy, so one tenant can never load another tenant's data.</summary>
public interface IProductRepository
{
    /// <summary>Loads the whole aggregate, including its sale units.</summary>
    Task<Product?> GetByIdAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken);

    /// <summary>Whole aggregates (with sale units) of the given Ids that belong to the pharmacy; unknown Ids are skipped.</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>Returns which of the given barcodes are already used by any product of the pharmacy.</summary>
    Task<IReadOnlyList<string>> GetExistingBarcodesAsync(
        Guid pharmacyId, IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);

    void Add(Product product);
}
