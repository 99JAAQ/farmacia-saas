using Farmacia.Domain.Common;

namespace Farmacia.Domain.Sales;

/// <summary>
/// One line of the ticket: a product in one sale unit. Price, VAT rate and conversion factor are copies taken at the
/// moment of the sale, so the ticket never changes when the catalog does. Created only through <see cref="Sale"/>.
/// </summary>
public class SaleItem : TenantEntity
{
    private readonly List<SaleItemBatch> _batches = [];

    private SaleItem() { } // EF Core

    internal SaleItem(
        Guid pharmacyId, Guid saleId, Guid productId, Guid saleUnitId, int quantity, decimal unitPrice,
        VatRate vatRate, int conversionFactor, bool requiresPrescription)
        : base(pharmacyId)
    {
        SaleId = saleId;
        ProductId = productId;
        SaleUnitId = saleUnitId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
        ConversionFactor = conversionFactor;
        RequiresPrescription = requiresPrescription;
    }

    public Guid SaleId { get; private init; }
    public Guid ProductId { get; private init; }
    public Guid SaleUnitId { get; private init; }

    /// <summary>In sale units (e.g. 2 boxes).</summary>
    public int Quantity { get; private init; }

    /// <summary>Price of one sale unit, VAT included.</summary>
    public decimal UnitPrice { get; private init; }

    public VatRate VatRate { get; private init; }
    public int ConversionFactor { get; private init; }

    /// <summary>Copied from the product: the ticket keeps which lines needed the prescription that was checked.</summary>
    public bool RequiresPrescription { get; private init; }

    /// <summary>The batches the units came from (FEFO), with their cost.</summary>
    public IReadOnlyCollection<SaleItemBatch> Batches => _batches.AsReadOnly();

    public int BaseUnits => Quantity * ConversionFactor;

    /// <summary>VAT included.</summary>
    public decimal Total => Quantity * UnitPrice;

    public decimal Vat => VatRate.IncludedIn(Total);

    /// <summary>What the units cost the pharmacy: the real margin of the line is Total minus this.</summary>
    public decimal Cost => Money.Round(_batches.Sum(b => b.Quantity * b.UnitCost));

    internal void TakeFrom(Guid batchId, int quantity, decimal unitCost) =>
        _batches.Add(new SaleItemBatch(PharmacyId, Id, batchId, quantity, unitCost));
}

/// <summary>How many base units of a <see cref="SaleItem"/> came from one batch, and at what cost.</summary>
public class SaleItemBatch : TenantEntity
{
    private SaleItemBatch() { } // EF Core

    internal SaleItemBatch(Guid pharmacyId, Guid saleItemId, Guid batchId, int quantity, decimal unitCost)
        : base(pharmacyId)
    {
        SaleItemId = saleItemId;
        BatchId = batchId;
        Quantity = quantity;
        UnitCost = unitCost;
    }

    public Guid SaleItemId { get; private init; }
    public Guid BatchId { get; private init; }

    /// <summary>In base units.</summary>
    public int Quantity { get; private init; }

    /// <summary>The batch's cost per base unit.</summary>
    public decimal UnitCost { get; private init; }
}
