using Farmacia.Domain.Common;

namespace Farmacia.Domain.Products;

/// <summary>A way of selling a product (box, blister, tablet). Created only through <see cref="Product"/>.</summary>
public class SaleUnit : TenantEntity
{
    private SaleUnit() { } // EF Core

    internal SaleUnit(Guid pharmacyId, Guid productId, string name, int conversionFactor, decimal price, string? barcode)
        : base(pharmacyId)
    {
        ProductId = productId;
        (Name, Barcode) = Validate(name, conversionFactor, price, barcode);
        ConversionFactor = conversionFactor;
        Price = price;
    }

    public Guid ProductId { get; private init; }
    public string Name { get; private set; } = null!;

    /// <summary>How many base units this sale unit contains (e.g. a box of 30 tablets = 30).</summary>
    public int ConversionFactor { get; private set; }

    public decimal Price { get; private set; }
    public string? Barcode { get; private set; }

    /// <summary>The rules of a presentation, shared by creating and editing it. Returns the cleaned name and barcode.</summary>
    internal static (string Name, string? Barcode) Validate(string name, int conversionFactor, decimal price, string? barcode)
    {
        if (conversionFactor < 1)
            throw new DomainException("El factor de conversión debe ser al menos 1.");

        if (price <= 0)
            throw new DomainException("El precio debe ser mayor que cero.");

        return (Guard.Required(name, "El nombre de la unidad de venta"), Guard.Optional(barcode));
    }

    /// <summary>Only through <see cref="Product.UpdateSaleUnit"/>, which validated it and checked it is unique.</summary>
    internal void Update(string name, int conversionFactor, decimal price, string? barcode)
    {
        Name = name;
        ConversionFactor = conversionFactor;
        Price = price;
        Barcode = barcode;
    }
}
