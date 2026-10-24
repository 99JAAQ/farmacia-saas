using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Purchases;
using MediatR;

namespace Farmacia.Application.Purchases.CreatePurchaseDraft;

/// <summary>Starts typing in a supplier invoice. Returns the new purchase Id.</summary>
public sealed record CreatePurchaseDraftCommand(
    Guid SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    PaymentTerm PaymentTerm,
    DateOnly? DueDate) : IRequest<Guid>;

public sealed class CreatePurchaseDraftHandler(
    IPurchaseRepository purchases,
    ISupplierRepository suppliers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<CreatePurchaseDraftCommand, Guid>
{
    public async Task<Guid> Handle(CreatePurchaseDraftCommand command, CancellationToken cancellationToken)
    {
        var purchase = Purchase.CreateDraft(
            currentPharmacy.PharmacyId, command.SupplierId, command.InvoiceNumber, command.InvoiceDate,
            command.PaymentTerm, command.DueDate);

        await suppliers.EnsureSupplierExistsAsync(purchase.PharmacyId, purchase.SupplierId, cancellationToken);
        await purchases.EnsureInvoiceIsNotRegisteredAsync(purchase, cancellationToken);

        purchases.Add(purchase);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }
}
