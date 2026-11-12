using Farmacia.Domain.Common;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Products;

/// <summary>What the API returns: plain data, never the domain entity itself.</summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string BaseUnitName,
    bool RequiresPrescription,
    VatRate VatRate,
    string? ActiveIngredient,
    string? Concentration,
    string? PharmaceuticalForm,
    string? SanitaryRegistration,
    Guid? CategoryId,
    bool IsActive,
    IReadOnlyList<SaleUnitDto> SaleUnits,
    int? MinimumStock)
{
    public static ProductDto From(Product product) => new(
        product.Id,
        product.Name,
        product.BaseUnitName,
        product.RequiresPrescription,
        product.VatRate,
        product.ActiveIngredient,
        product.Concentration,
        product.PharmaceuticalForm,
        product.SanitaryRegistration,
        product.CategoryId,
        product.IsActive,
        product.SaleUnits
            .OrderByDescending(u => u.ConversionFactor)
            .Select(u => new SaleUnitDto(u.Id, u.Name, u.ConversionFactor, u.Price, u.Barcode))
            .ToList(),
        product.MinimumStock);
}

public sealed record SaleUnitDto(Guid Id, string Name, int ConversionFactor, decimal Price, string? Barcode);
