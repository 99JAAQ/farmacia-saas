using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Suppliers.SearchSuppliers;

public sealed record SearchSuppliersQuery(string? Search) : IRequest<IReadOnlyList<SupplierDto>>;

public sealed class SearchSuppliersHandler(
    ISupplierRepository suppliers,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<SearchSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    // A pick list, not a report: enough to find one by typing a few letters.
    private const int MaxResults = 20;

    public async Task<IReadOnlyList<SupplierDto>> Handle(SearchSuppliersQuery query, CancellationToken cancellationToken)
    {
        var found = await suppliers.SearchAsync(currentPharmacy.PharmacyId, query.Search, MaxResults, cancellationToken);
        return found.Select(SupplierDto.From).ToList();
    }
}
