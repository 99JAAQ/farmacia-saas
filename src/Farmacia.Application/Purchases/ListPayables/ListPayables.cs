using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Purchases.ListPayables;

/// <summary>The credit invoices still owed, the most overdue first; optionally of one supplier.</summary>
public sealed record ListPayablesQuery(Guid? SupplierId) : IRequest<IReadOnlyList<PayableInvoiceDto>>;

public sealed class ListPayablesHandler(
    IPurchaseRepository purchases,
    ICurrentPharmacy currentPharmacy,
    PurchaseReader reader) : IRequestHandler<ListPayablesQuery, IReadOnlyList<PayableInvoiceDto>>
{
    public async Task<IReadOnlyList<PayableInvoiceDto>> Handle(ListPayablesQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var invoices = await purchases.ListCreditInvoicesAsync(pharmacyId, query.SupplierId, cancellationToken);
        return await reader.ToPayablesAsync(pharmacyId, invoices, cancellationToken);
    }
}
