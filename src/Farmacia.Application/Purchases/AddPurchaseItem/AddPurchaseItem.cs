using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Common;
using MediatR;

namespace Farmacia.Application.Purchases.AddPurchaseItem;

/// <summary>Adds one invoice line to a draft. It is saved right away, so a power cut does not lose it. Returns the line Id.</summary>
public sealed record AddPurchaseItemCommand(
    Guid PurchaseId,
    Guid ProductId,
    Guid SaleUnitId,
    int Quantity,
    int BonusQuantity,
    decimal UnitCost,
    decimal LineDiscount,
    VatRate VatRate,
    string LotNumber,
    DateOnly ExpirationDate) : IRequest<Guid>;

public sealed class AddPurchaseItemHandler(
    IPurchaseRepository purchases,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<AddPurchaseItemCommand, Guid>
{
    public async Task<Guid> Handle(AddPurchaseItemCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var purchase = await purchases.GetOrThrowAsync(pharmacyId, command.PurchaseId, cancellationToken);

        var product = await products.GetByIdAsync(pharmacyId, command.ProductId, cancellationToken)
            ?? throw new ValidationException("El producto indicado no existe.");

        if (!product.IsActive)
            throw new ValidationException($"{product.Name} está inactivo: no se compra. Reactívelo en el catálogo si vuelve a manejarse.");

        var saleUnit = product.SaleUnits.SingleOrDefault(u => u.Id == command.SaleUnitId)
            ?? throw new ValidationException($"La presentación indicada no pertenece a {product.Name}.");

        // The line keeps a copy of the conversion factor: the purchase must not change if the presentation does.
        var item = purchase.AddItem(
            product.Id, saleUnit.Id, saleUnit.ConversionFactor, command.Quantity, command.BonusQuantity,
            command.UnitCost, command.LineDiscount, command.VatRate, command.LotNumber, command.ExpirationDate);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
