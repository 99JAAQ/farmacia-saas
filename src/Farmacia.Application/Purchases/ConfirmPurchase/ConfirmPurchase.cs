using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Purchases.ConfirmPurchase;

/// <summary>Receives the goods: the purchase is locked and its batches enter the inventory.</summary>
/// <param name="PrintedInvoiceTotal">The total printed on the paper invoice, to catch typing errors.</param>
public sealed record ConfirmPurchaseCommand(Guid PurchaseId, decimal PrintedInvoiceTotal) : IRequest<PurchaseDto>;

public sealed class ConfirmPurchaseHandler(
    IPurchaseRepository purchases,
    IPharmacyRepository pharmacies,
    IBatchRepository batches,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    IClock clock,
    PurchaseReader reader) : IRequestHandler<ConfirmPurchaseCommand, PurchaseDto>
{
    public async Task<PurchaseDto> Handle(ConfirmPurchaseCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var purchase = await purchases.GetOrThrowAsync(pharmacyId, command.PurchaseId, cancellationToken);
        var pharmacy = await pharmacies.GetByIdAsync(pharmacyId, cancellationToken)
            ?? throw new NotFoundException("La farmacia no existe.");

        // A VAT-responsible pharmacy recovers the purchase VAT, so it is not part of the inventory cost.
        var receivedBatches = purchase.Confirm(
            command.PrintedInvoiceTotal, vatIsPartOfCost: !pharmacy.IsVatResponsible, clock.Now);

        foreach (var batch in receivedBatches)
            batches.Add(batch);

        // Purchase, batches and their kardex movements: one SaveChanges, one transaction. All or nothing.
        // A second confirmation running at the same time fails here with a 409 (RowVersion on the purchase).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await reader.ToDtoAsync(purchase, cancellationToken);
    }
}
