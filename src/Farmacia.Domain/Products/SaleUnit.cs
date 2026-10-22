using Farmacia.Domain.Common;

namespace Farmacia.Domain.Products;

/// <summary>A way of selling a product (box, blister, tablet). Created only through <see cref="Product"/>.</summary>
public class SaleUnit : TenantEntity
{
    private SaleUnit() { } // EF Core

    internal SaleUnit(Guid pharmacyId, Guid productId, string name, int conversionFactor, decimal price, string? barcode)
        : base(pharmacyId)
    {
        if (conversionFactor < 1)
            throw new DomainException("El factor de conversión debe ser al menos 1.");

        if (price <= 0)
            throw new DomainException("El precio debe ser mayor que cero.");

        ProductId = productId;
        Name = Guard.Required(name, "El nombre de la unidad de venta");
        ConversionFactor = conversionFactor;
        Price = price;
        Barcode = Guard.Optional(barcode);
    }

    public Guid ProductId { get; private init; }
    public string Name { get; private set; } = null!;

    /// <summary>How many base units this sale unit contains (e.g. a box of 30 tablets = 30).</summary>
    public int ConversionFactor { get; private set; }

    public decimal Price { get; private set; }
    public string? Barcode { get; private set; }
}
