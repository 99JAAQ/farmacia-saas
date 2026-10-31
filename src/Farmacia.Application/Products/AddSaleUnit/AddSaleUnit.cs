using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.AddSaleUnit;

/// <summary>A new presentation, e.g. selling loose tablets of a product sold by the box.</summary>
public sealed record AddSaleUnitCommand(Guid ProductId, string Name, int ConversionFactor, decimal Price, string? Barcode)
    : IRequest<Guid>;

public sealed class AddSaleUnitHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<AddSaleUnitCommand, Guid>
{
    public async Task<Guid> Handle(AddSaleUnitCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetOrThrowAsync(currentPharmacy.PharmacyId, command.ProductId, cancellationToken);

        await products.EnsureBarcodeFreeAsync(product, command.Barcode, cancellationToken);
        var unit = product.AddSaleUnit(command.Name, command.ConversionFactor, command.Price, command.Barcode);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return unit.Id;
    }
}
