using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.SearchProducts;

/// <summary>For the POS and the purchase lines: by name, active ingredient or scanned barcode.</summary>
/// <param name="IncludeInactive">Discontinued products too (the catalog, to reactivate them). Never for selling.</param>
public sealed record SearchProductsQuery(string? Search, bool IncludeInactive = false) : IRequest<IReadOnlyList<ProductDto>>;

public sealed class SearchProductsHandler(
    IProductRepository products,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductDto>>
{
    private const int MaxResults = 20;

    public async Task<IReadOnlyList<ProductDto>> Handle(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        var found = await products.SearchAsync(
            currentPharmacy.PharmacyId, query.Search, MaxResults, cancellationToken, query.IncludeInactive);
        return found.Select(ProductDto.From).ToList();
    }
}
