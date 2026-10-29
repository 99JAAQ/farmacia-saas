using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Products;
using MediatR;

namespace Farmacia.Application.Products.CreateProduct;

public sealed class CreateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        if (command.SaleUnits is not { Count: > 0 })
            throw new ValidationException("El producto debe tener al menos una unidad de venta.");

        // 1. Build the aggregate in memory: the domain validates its own rules, no database needed.
        var product = Product.Create(
            pharmacyId, command.Name, command.BaseUnitName, command.RequiresPrescription,
            command.ActiveIngredient, command.Concentration, command.PharmaceuticalForm,
            command.SanitaryRegistration, command.CategoryId, command.VatRate);

        foreach (var unit in command.SaleUnits)
            product.AddSaleUnit(unit.Name, unit.ConversionFactor, unit.Price, unit.Barcode);

        // 2. Rules that need the database.
        if (command.CategoryId is { } categoryId && !await categories.ExistsAsync(pharmacyId, categoryId, cancellationToken))
            throw new ValidationException("La categoría indicada no existe.");

        var barcodes = product.SaleUnits.Select(u => u.Barcode).OfType<string>().ToList();
        if (barcodes.Count > 0)
        {
            var taken = await products.GetExistingBarcodesAsync(pharmacyId, barcodes, cancellationToken);
            if (taken.Count > 0)
                throw new ConflictException($"Códigos de barras ya asignados a otro producto: {string.Join(", ", taken)}.");
        }

        // 3. One commit for the whole use case.
        products.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
