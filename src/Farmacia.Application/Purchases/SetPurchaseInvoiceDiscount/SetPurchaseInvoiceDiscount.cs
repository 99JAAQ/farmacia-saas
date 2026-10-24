using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Purchases.SetPurchaseInvoiceDiscount;

public sealed record SetPurchaseInvoiceDiscountCommand(Guid PurchaseId, decimal Discount) : IRequest;

public sealed class SetPurchaseInvoiceDiscountHandler(
    IPurchaseRepository purchases,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<SetPurchaseInvoiceDiscountCommand>
{
    public async Task Handle(SetPurchaseInvoiceDiscountCommand command, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetOrThrowAsync(currentPharmacy.PharmacyId, command.PurchaseId, cancellationToken);

        purchase.SetInvoiceDiscount(command.Discount);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
