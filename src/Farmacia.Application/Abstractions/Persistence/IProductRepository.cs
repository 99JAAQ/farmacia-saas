using Farmacia.Domain.Products;

namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>Every read is scoped to a pharmacy, so one tenant can never load another tenant's data.</summary>
public interface IProductRepository
{
    /// <summary>Loads the whole aggregate, including its sale units.</summary>
    Task<Product?> GetByIdAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken);

    /// <summary>Active products with a minimum stock set (the candidates for a low-stock alert), with their presentations.</summary>
    Task<IReadOnlyList<Product>> ListWithMinimumStockAsync(Guid pharmacyId, CancellationToken cancellationToken);

    /// <summary>Whole aggregates (with sale units) of the given Ids that belong to the pharmacy; unknown Ids are skipped.</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>
    /// By name or active ingredient (ignoring case and accents) or by exact barcode, alphabetically, with sale units.
    /// Empty search: the first ones. Discontinued (inactive) products only if asked: the POS and purchases never offer them.
    /// </summary>
    Task<IReadOnlyList<Product>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken, bool includeInactive = false);

    /// <summary>Returns which of the given barcodes are already used by any product of the pharmacy.</summary>
    Task<IReadOnlyList<string>> GetExistingBarcodesAsync(
        Guid pharmacyId, IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);

    void Add(Product product);
}
