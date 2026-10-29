using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Sales.ListSales;

/// <summary>A shift's sales, newest first: the POS shows them to reprint a ticket.</summary>
public sealed record ListSalesQuery(Guid CashSessionId) : IRequest<IReadOnlyList<SaleSummaryDto>>;

public sealed class ListSalesHandler(
    ISaleRepository sales,
    ICurrentPharmacy currentPharmacy,
    SaleReader reader) : IRequestHandler<ListSalesQuery, IReadOnlyList<SaleSummaryDto>>
{
    public async Task<IReadOnlyList<SaleSummaryDto>> Handle(ListSalesQuery query, CancellationToken cancellationToken)
    {
        // A shift of another pharmacy simply has no sales here: the pharmacy filter is part of the query.
        var pharmacyId = currentPharmacy.PharmacyId;
        var list = await sales.ListByCashSessionAsync(pharmacyId, query.CashSessionId, cancellationToken);
        return await reader.ToSummariesAsync(pharmacyId, list, cancellationToken);
    }
}
