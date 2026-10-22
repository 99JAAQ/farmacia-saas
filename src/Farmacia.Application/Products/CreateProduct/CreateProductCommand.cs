using MediatR;

namespace Farmacia.Application.Products.CreateProduct;

/// <summary>Creates a product with its sale units. Returns the new product's Id.</summary>
public sealed record CreateProductCommand(
    string Name,
    string BaseUnitName,
    bool RequiresPrescription,
    string? ActiveIngredient,
    string? Concentration,
    string? PharmaceuticalForm,
    string? SanitaryRegistration,
    Guid? CategoryId,
    IReadOnlyList<CreateSaleUnitItem> SaleUnits) : IRequest<Guid>;

public sealed record CreateSaleUnitItem(string Name, int ConversionFactor, decimal Price, string? Barcode);
