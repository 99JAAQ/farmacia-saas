using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.SetProductActive;

/// <summary>Discontinues a product (it stops being sold and bought) or brings it back. Never deletes it.</summary>
public sealed record SetProductActiveCommand(Guid ProductId, bool Active) : IRequest;

public sealed class SetProductActiveHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<SetProductActiveCommand>
{
    public async Task Handle(SetProductActiveCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetOrThrowAsync(currentPharmacy.PharmacyId, command.ProductId, cancellationToken);

        if (command.Active)
            product.Activate();
        else
            product.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
