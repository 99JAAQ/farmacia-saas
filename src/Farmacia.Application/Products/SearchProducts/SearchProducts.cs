using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.SearchProducts;

/// <summary>For the purchase lines and, later, the POS: by name, active ingredient or scanned barcode.</summary>
public sealed record SearchProductsQuery(string? Search) : IRequest<IReadOnlyList<ProductDto>>;

public sealed class SearchProductsHandler(
    IProductRepository products,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductDto>>
{
    private const int MaxResults = 20;

    public async Task<IReadOnlyList<ProductDto>> Handle(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        var found = await products.SearchAsync(currentPharmacy.PharmacyId, query.Search, MaxResults, cancellationToken);
        return found.Select(ProductDto.From).ToList();
    }
}
