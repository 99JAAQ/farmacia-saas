using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Purchases;
using MediatR;

namespace Farmacia.Application.Purchases.ListPurchases;

/// <summary>Most recent purchases; with Status = Draft, the invoices left half-typed (e.g. by a power cut).</summary>
public sealed record ListPurchasesQuery(PurchaseStatus? Status) : IRequest<IReadOnlyList<PurchaseSummaryDto>>;

public sealed class ListPurchasesHandler(
    IPurchaseRepository purchases,
    ICurrentPharmacy currentPharmacy,
    PurchaseReader reader) : IRequestHandler<ListPurchasesQuery, IReadOnlyList<PurchaseSummaryDto>>
{
    // Enough for the screen; real paging comes with the purchase history report.
    private const int MaxResults = 50;

    public async Task<IReadOnlyList<PurchaseSummaryDto>> Handle(ListPurchasesQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var list = await purchases.ListAsync(pharmacyId, query.Status, MaxResults, cancellationToken);
        return await reader.ToSummariesAsync(pharmacyId, list, cancellationToken);
    }
}
