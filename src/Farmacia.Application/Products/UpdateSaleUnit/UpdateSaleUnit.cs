using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.UpdateSaleUnit;

/// <summary>A new price applies to the next sales only; a new factor does not change the stock (it is in base units).</summary>
public sealed record UpdateSaleUnitCommand(
    Guid ProductId, Guid SaleUnitId, string Name, int ConversionFactor, decimal Price, string? Barcode) : IRequest;

public sealed class UpdateSaleUnitHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<UpdateSaleUnitCommand>
{
    public async Task Handle(UpdateSaleUnitCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetOrThrowAsync(currentPharmacy.PharmacyId, command.ProductId, cancellationToken);

        await products.EnsureBarcodeFreeAsync(product, command.Barcode, cancellationToken);
        product.UpdateSaleUnit(command.SaleUnitId, command.Name, command.ConversionFactor, command.Price, command.Barcode);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
