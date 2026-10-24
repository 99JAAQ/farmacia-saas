using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Purchases.RemovePurchaseItem;

public sealed record RemovePurchaseItemCommand(Guid PurchaseId, Guid ItemId) : IRequest;

public sealed class RemovePurchaseItemHandler(
    IPurchaseRepository purchases,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<RemovePurchaseItemCommand>
{
    public async Task Handle(RemovePurchaseItemCommand command, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetOrThrowAsync(currentPharmacy.PharmacyId, command.PurchaseId, cancellationToken);

        purchase.RemoveItem(command.ItemId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
