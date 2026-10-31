using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.GetCatalog;

/// <summary>The catalog screen: products with their stock, inactive ones included if asked.</summary>
public sealed record GetCatalogQuery(string? Search, bool IncludeInactive) : IRequest<IReadOnlyList<CatalogItemDto>>;

/// <param name="SellableUnits">In base units (e.g. tablets), in batches that have not expired.</param>
/// <param name="ExpiredUnits">In expired batches: they cannot be sold, only written off.</param>
/// <param name="NextExpiration">The nearest expiration among the sellable batches.</param>
public sealed record CatalogItemDto(ProductDto Product, int SellableUnits, int ExpiredUnits, DateOnly? NextExpiration);

public sealed class GetCatalogHandler(
    IProductRepository products,
    IBatchRepository batches,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<GetCatalogQuery, IReadOnlyList<CatalogItemDto>>
{
    // A screen, not a report: enough to find a product by typing a few letters.
    private const int MaxResults = 50;

    public async Task<IReadOnlyList<CatalogItemDto>> Handle(GetCatalogQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var found = await products.SearchAsync(pharmacyId, query.Search, MaxResults, cancellationToken, query.IncludeInactive);

        // The pharmacy's date (Colombia), not the server's: a batch expiring today can still be sold today.
        var today = DateOnly.FromDateTime(clock.Now.DateTime);
        var stock = (await batches.GetStockAsync(pharmacyId, found.Select(p => p.Id).ToList(), today, cancellationToken))
            .ToDictionary(s => s.ProductId);

        return found
            .Select(p =>
            {
                var s = stock.GetValueOrDefault(p.Id);
                return new CatalogItemDto(ProductDto.From(p), s?.SellableUnits ?? 0, s?.ExpiredUnits ?? 0, s?.NextExpiration);
            })
            .ToList();
    }
}
