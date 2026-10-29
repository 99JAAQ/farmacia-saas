using Farmacia.Domain.Common;
using MediatR;

namespace Farmacia.Application.Products.CreateProduct;

/// <summary>Creates a product with its sale units. Returns the new product's Id.</summary>
/// <param name="VatRate">VAT included in the sale prices; most medicines are excluded (0%), the default.</param>
public sealed record CreateProductCommand(
    string Name,
    string BaseUnitName,
    bool RequiresPrescription,
    string? ActiveIngredient,
    string? Concentration,
    string? PharmaceuticalForm,
    string? SanitaryRegistration,
    Guid? CategoryId,
    IReadOnlyList<CreateSaleUnitItem> SaleUnits,
    VatRate VatRate = VatRate.Exempt) : IRequest<Guid>;

public sealed record CreateSaleUnitItem(string Name, int ConversionFactor, decimal Price, string? Barcode);
