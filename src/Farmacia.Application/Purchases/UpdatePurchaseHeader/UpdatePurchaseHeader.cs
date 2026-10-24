using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Purchases;
using MediatR;

namespace Farmacia.Application.Purchases.UpdatePurchaseHeader;

public sealed record UpdatePurchaseHeaderCommand(
    Guid PurchaseId,
    Guid SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    PaymentTerm PaymentTerm,
    DateOnly? DueDate) : IRequest;

public sealed class UpdatePurchaseHeaderHandler(
    IPurchaseRepository purchases,
    ISupplierRepository suppliers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<UpdatePurchaseHeaderCommand>
{
    public async Task Handle(UpdatePurchaseHeaderCommand command, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetOrThrowAsync(currentPharmacy.PharmacyId, command.PurchaseId, cancellationToken);

        purchase.UpdateHeader(
            command.SupplierId, command.InvoiceNumber, command.InvoiceDate, command.PaymentTerm, command.DueDate);

        await suppliers.EnsureSupplierExistsAsync(purchase.PharmacyId, purchase.SupplierId, cancellationToken);
        await purchases.EnsureInvoiceIsNotRegisteredAsync(purchase, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
