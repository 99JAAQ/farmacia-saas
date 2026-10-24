using Farmacia.Domain.Common;

namespace Farmacia.Domain.Purchases;

/// <summary>
/// One line of a supplier invoice: one product, one lot. Created only through <see cref="Purchase"/>.
/// Quantities are in purchase packages (e.g. boxes); <see cref="UnitsPerPackage"/> converts them to base units.
/// </summary>
public class PurchaseItem : TenantEntity
{
    private PurchaseItem() { } // EF Core

    internal PurchaseItem(
        Guid pharmacyId, Guid purchaseId, Guid productId, Guid saleUnitId, int unitsPerPackage,
        int quantity, int bonusQuantity, decimal unitCost, decimal lineDiscount, VatRate vatRate,
        string lotNumber, DateOnly expirationDate)
        : base(pharmacyId)
    {
        if (productId == Guid.Empty || saleUnitId == Guid.Empty)
            throw new DomainException("El producto y su presentación son obligatorios.");

        if (unitsPerPackage < 1)
            throw new DomainException("La presentación debe tener al menos 1 unidad.");

        if (quantity < 0 || bonusQuantity < 0)
            throw new DomainException("Las cantidades no pueden ser negativas.");

        if (quantity + bonusQuantity < 1)
            throw new DomainException("La línea debe tener al menos una unidad comprada o bonificada.");

        if (unitCost < 0)
            throw new DomainException("El costo no puede ser negativo.");

        if (lineDiscount < 0 || lineDiscount > quantity * unitCost)
            throw new DomainException("El descuento de la línea no puede ser negativo ni mayor que su valor.");

        if (!Enum.IsDefined(vatRate))
            throw new DomainException("La tarifa de IVA no es válida.");

        PurchaseId = purchaseId;
        ProductId = productId;
        SaleUnitId = saleUnitId;
        UnitsPerPackage = unitsPerPackage;
        Quantity = quantity;
        BonusQuantity = bonusQuantity;
        UnitCost = unitCost;
        LineDiscount = lineDiscount;
        VatRate = vatRate;
        LotNumber = Guard.Required(lotNumber, "El número de lote");
        ExpirationDate = expirationDate;
    }

    public Guid PurchaseId { get; private init; }
    public Guid ProductId { get; private init; }

    /// <summary>The presentation the supplier invoiced (e.g. "Caja x30").</summary>
    public Guid SaleUnitId { get; private init; }

    /// <summary>
    /// Copy of the presentation's conversion factor at purchase time: the purchase is a historical document
    /// and must not change if the presentation is edited later.
    /// </summary>
    public int UnitsPerPackage { get; private init; }

    public int Quantity { get; private init; }

    /// <summary>Free packages from the supplier ("pague 10, lleve 12" = 10 bought + 2 bonus).</summary>
    public int BonusQuantity { get; private init; }

    /// <summary>Cost per package as printed on the invoice, before discounts and VAT.</summary>
    public decimal UnitCost { get; private init; }

    public decimal LineDiscount { get; private init; }
    public VatRate VatRate { get; private init; }
    public string LotNumber { get; private init; } = null!;
    public DateOnly ExpirationDate { get; private init; }

    /// <summary>Set on confirmation: the batch this line created.</summary>
    public Guid? BatchId { get; private set; }

    /// <summary>Set on confirmation: the real cost of each base unit, after bonus and discounts.</summary>
    public decimal? UnitCostPerBaseUnit { get; private set; }

    public int BaseUnits => (Quantity + BonusQuantity) * UnitsPerPackage;
    public decimal GrossAmount => Quantity * UnitCost;
    public decimal NetAmount => GrossAmount - LineDiscount;

    internal void MarkReceived(Guid batchId, decimal unitCostPerBaseUnit)
    {
        BatchId = batchId;
        UnitCostPerBaseUnit = unitCostPerBaseUnit;
    }
}
