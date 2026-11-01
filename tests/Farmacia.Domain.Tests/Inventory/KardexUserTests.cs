using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Tests.Inventory;

/// <summary>Every kardex entry says who caused it: for a write-off or an adjustment it is the first thing the owner asks.</summary>
public class KardexUserTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid Keeper = Guid.CreateVersion7();
    private static readonly Guid Cashier = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly Product _product = Product.Create(PharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
    private readonly SaleUnit _box;

    public KardexUserTests() => _box = _product.AddSaleUnit("Caja x30", 30, 12_000m);

    [Fact]
    public void ConfirmingAPurchase_RecordsWhoReceivedTheGoods()
    {
        var purchase = Purchase.CreateDraft(PharmacyId, Guid.CreateVersion7(), "FE-1", new DateOnly(2026, 10, 30), PaymentTerm.Cash);
        purchase.AddItem(_product.Id, _box.Id, 30, 1, 0, 9_000m, 0m, VatRate.Exempt, "L1", new DateOnly(2028, 1, 31));

        var batch = Assert.Single(purchase.Confirm(9_000m, vatIsPartOfCost: false, Now, confirmedBy: Keeper));

        Assert.Equal(Keeper, Assert.Single(batch.PendingMovements).PerformedBy);
    }

    [Fact]
    public void ASaleAndItsReturn_RecordTheirCashier()
    {
        var batch = Batch.Receive(PharmacyId, _product.Id, "L1", new DateOnly(2028, 1, 31), 90, 250m, Now.AddDays(-1), receivedBy: Keeper);
        var sale = Sale.Register(PharmacyId, 1, Guid.CreateVersion7(), Cashier, Now,
            [new SaleLine(_product, _box.Id, 1, [batch])], [new PaymentLine(PaymentMethod.Cash, 12_000m)], false);

        SaleReturn.Register(1, sale, [], Guid.CreateVersion7(), Cashier, Now.AddHours(1), "No le sirvió",
            [new ReturnLine(sale.Items.Single().Id, 1, ReturnDisposition.Restock)], [batch]);

        Assert.Equal(
            [(StockMovementType.Purchase, Keeper), (StockMovementType.Sale, Cashier), (StockMovementType.CustomerReturn, Cashier)],
            batch.PendingMovements.Select(m => (m.Type, m.PerformedBy!.Value)));
    }

    [Fact]
    public void AWriteOffAndAnAdjustment_RecordWhoDidThem()
    {
        var expired = Batch.Receive(PharmacyId, _product.Id, "VIEJO", new DateOnly(2026, 10, 30), 20, 250m, Now.AddDays(-60));
        var counted = Batch.Receive(PharmacyId, _product.Id, "L2", new DateOnly(2028, 1, 31), 50, 250m, Now.AddDays(-5));

        expired.WriteOffExpired(Now, writtenOffBy: Keeper);
        counted.AdjustToCount(48, "Dos tabletas dañadas", Now, adjustedBy: Keeper);

        Assert.Equal((StockMovementType.ExpiredWriteOff, -20, Keeper), Last(expired));
        Assert.Equal((StockMovementType.Adjustment, -2, Keeper), Last(counted));
    }

    private static (StockMovementType, int, Guid?) Last(Batch batch)
    {
        var movement = batch.PendingMovements.Last();
        return (movement.Type, movement.QuantityChange, movement.PerformedBy);
    }
}
