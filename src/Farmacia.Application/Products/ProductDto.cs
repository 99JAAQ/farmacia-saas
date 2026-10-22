using Farmacia.Domain.Products;

namespace Farmacia.Application.Products;

/// <summary>What the API returns: plain data, never the domain entity itself.</summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string BaseUnitName,
    bool RequiresPrescription,
    string? ActiveIngredient,
    string? Concentration,
    string? PharmaceuticalForm,
    string? SanitaryRegistration,
    Guid? CategoryId,
    IReadOnlyList<SaleUnitDto> SaleUnits)
{
    public static ProductDto From(Product product) => new(
        product.Id,
        product.Name,
        product.BaseUnitName,
        product.RequiresPrescription,
        product.ActiveIngredient,
        product.Concentration,
        product.PharmaceuticalForm,
        product.SanitaryRegistration,
        product.CategoryId,
        product.SaleUnits
            .OrderByDescending(u => u.ConversionFactor)
            .Select(u => new SaleUnitDto(u.Id, u.Name, u.ConversionFactor, u.Price, u.Barcode))
            .ToList());
}

public sealed record SaleUnitDto(Guid Id, string Name, int ConversionFactor, decimal Price, string? Barcode);
