using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Products.SetMinimumStock;

/// <param name="MinimumStock">In base units (the screen converts from the presentation the user picks); null removes it.</param>
public sealed record SetMinimumStockCommand(Guid ProductId, int? MinimumStock) : IRequest;

public sealed class SetMinimumStockHandler(IProductRepository products, IUnitOfWork unitOfWork, ICurrentPharmacy currentPharmacy)
    : IRequestHandler<SetMinimumStockCommand>
{
    public async Task Handle(SetMinimumStockCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(currentPharmacy.PharmacyId, command.ProductId, cancellationToken)
            ?? throw new NotFoundException("No se encontró el producto.");

        product.SetMinimumStock(command.MinimumStock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
