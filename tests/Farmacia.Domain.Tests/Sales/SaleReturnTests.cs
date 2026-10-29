using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Tests.Sales;

public class SaleReturnTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid SaleShiftId = Guid.CreateVersion7();
    private static readonly Guid ReturnShiftId = Guid.CreateVersion7();
    private static readonly Guid CashierId = Guid.CreateVersion7();
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset SoldAt = new(2026, 10, 28, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset ReturnedAt = SoldAt.AddDays(2);

    // Acetaminofén: box of 30 at 12.000; ibuprofeno: box of 20 at 8.000.
    private readonly Product _acetaminophen = Product.Create(PharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
    private readonly Product _ibuprofen = Product.Create(PharmacyId, "Ibuprofeno 400 mg", "Tableta", requiresPrescription: false);
    private readonly SaleUnit _box;
    private readonly SaleUnit _ibuprofenBox;

    public SaleReturnTests()
    {
        _box = _acetaminophen.AddSaleUnit("Caja x30", 30, 12_000m);
        _ibuprofenBox = _ibuprofen.AddSaleUnit("Caja x20", 20, 8_000m);
    }

    private static Batch Lot(Product product, string lot, int quantity, DateOnly expiration, decimal unitCost = 250m) =>
        Batch.Receive(PharmacyId, product.Id, lot, expiration, quantity, unitCost, ReceivedAt);

    private static Sale Sell(params SaleLine[] lines) =>
        Sale.Register(PharmacyId, 1, SaleShiftId, CashierId, SoldAt, lines, [new PaymentLine(PaymentMethod.Cash, 1_000_000m)], false);

    private static SaleReturn Return(
        Sale sale, IReadOnlyList<ReturnLine> lines, IReadOnlyList<Batch> batches,
        IReadOnlyList<SaleReturn>? previous = null, DateTimeOffset? at = null, string reason = "El cliente se equivocó", long number = 1) =>
        SaleReturn.Register(number, sale, previous ?? [], ReturnShiftId, CashierId, at ?? ReturnedAt, reason, lines, batches);

    private static ReturnLine Restock(SaleItem item, int quantity) => new(item.Id, quantity, ReturnDisposition.Restock);

    private static ReturnLine Discard(SaleItem item, int quantity) => new(item.Id, quantity, ReturnDisposition.Discard);

    // ---------- Restock ----------

    [Fact]
    public void Register_APartialReturn_PutsTheUnitsBackIntoTheirBatch_WithAKardexMovement_AndRefundsWhatWasPaid()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));
        var sale = Sell(new SaleLine(_acetaminophen, _box.Id, 3, [batch]));
        var sold = sale.Items.Single();

        var saleReturn = Return(sale, [Restock(sold, 1)], [batch]);

        Assert.Equal(40, batch.Quantity); // 100 − 90 sold + 30 back
        var movement = batch.PendingMovements.Last();
        Assert.Equal((StockMovementType.CustomerReturn, 30, saleReturn.Id), (movement.Type, movement.QuantityChange, movement.ReferenceId));

        Assert.Equal(12_000m, saleReturn.Refund);
        Assert.Equal((sale.Id, ReturnShiftId, "El cliente se equivocó"), (saleReturn.SaleId, saleReturn.CashSessionId, saleReturn.Reason));
        var item = Assert.Single(saleReturn.Items);
        Assert.Equal((sold.Id, 1, 12_000m), (item.SaleItemId, item.Quantity, item.UnitPrice));
        Assert.Equal((batch.Id, 30, 250m), (item.Batches.Single().BatchId, item.Batches.Single().Quantity, item.Batches.Single().UnitCost));
    }

    [Fact]
    public void Register_PutsTheUnitsBackLatestExpiringFirst_SoReturningEverythingLeavesTheBatchesAsBefore()
    {
        var early = Lot(_acetaminophen, "A", 40, new DateOnly(2027, 1, 31));
        var late = Lot(_acetaminophen, "B", 100, new DateOnly(2028, 1, 31));
        var sale = Sell(new SaleLine(_acetaminophen, _box.Id, 2, [early, late])); // FEFO: 40 from A, 20 from B
        var sold = sale.Items.Single();
        Assert.Equal((0, 80), (early.Quantity, late.Quantity));

        var first = Return(sale, [Restock(sold, 1)], [early, late]);
        Assert.Equal((10, 100), (early.Quantity, late.Quantity)); // B takes back its 20 first, then A 10

        Return(sale, [Restock(sold, 1)], [early, late], previous: [first], number: 2);
        Assert.Equal((40, 100), (early.Quantity, late.Quantity)); // exactly as before the sale
    }

    // ---------- Discard ----------

    [Fact]
    public void Register_DiscardedUnits_AreRefunded_ButDoNotGoBackToStock()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));
        var sale = Sell(new SaleLine(_acetaminophen, _box.Id, 2, [batch]));
        var movementsBefore = batch.PendingMovements.Count;

        var saleReturn = Return(sale, [Discard(sale.Items.Single(), 2)], [batch]);

        Assert.Equal(40, batch.Quantity);
        Assert.Equal(movementsBefore, batch.PendingMovements.Count);
        Assert.Equal(24_000m, saleReturn.Refund);
        Assert.Empty(saleReturn.Items.Single().Batches);
    }

    [Fact]
    public void Register_IntoAnExpiredBatch_Throws_ButTheUnitsCanBeDiscarded()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2026, 10, 29));
        var sale = Sell(new SaleLine(_acetaminophen, _box.Id, 1, [batch]));
        var afterExpiration = new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.FromHours(-5));

        var ex = Assert.Throws<DomainException>(() => Return(sale, [Restock(sale.Items.Single(), 1)], [batch], at: afterExpiration));
        Assert.Contains("vencido", ex.Message);
        Assert.Equal(70, batch.Quantity);

        var discarded = Return(sale, [Discard(sale.Items.Single(), 1)], [batch], at: afterExpiration);
        Assert.Equal(12_000m, discarded.Refund);
    }

    // ---------- How much can be returned ----------

    [Fact]
    public void Register_CannotReturnMoreThanWasSold_CountingTheEarlierReturns()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));
        var sale = Sell(new SaleLine(_acetaminophen, _box.Id, 3, [batch]));
        var sold = sale.Items.Single();

        var first = Return(sale, [Discard(sold, 2)], [batch]);

        var tooMany = Assert.Throws<DomainException>(() => Return(sale, [Restock(sold, 2)], [batch], previous: [first]));
        Assert.Contains("solo quedan 1", tooMany.Message);

        var second = Return(sale, [Restock(sold, 1)], [batch], previous: [first], number: 2);

        var nothingLeft = Assert.Throws<DomainException>(() => Return(sale, [Discard(sold, 1)], [batch], previous: [first, second]));
        Assert.Contains("completa", nothingLeft.Message);
    }

    [Fact]
    public void Register_ChecksEveryLineFirst_SoAnInvalidLineLeavesEveryBatchUntouched()
    {
        var acetaminophenLot = Lot(_acetaminophen, "A1", 100, new DateOnly(2027, 6, 30));
        var ibuprofenLot = Lot(_ibuprofen, "I1", 100, new DateOnly(2027, 6, 30));
        var sale = Sell(
            new SaleLine(_acetaminophen, _box.Id, 1, [acetaminophenLot]),
            new SaleLine(_ibuprofen, _ibuprofenBox.Id, 1, [ibuprofenLot]));
        var (acetaminophenItem, ibuprofenItem) = (sale.Items.First(), sale.Items.Last());

        Assert.Throws<DomainException>(() =>
            Return(sale, [Restock(acetaminophenItem, 1), Restock(ibuprofenItem, 5)], [acetaminophenLot, ibuprofenLot]));

        Assert.Equal((70, 80), (acetaminophenLot.Quantity, ibuprofenLot.Quantity));
    }

    [Fact]
    public void Register_RefundsEachLineAtItsOwnPrice()
    {
        var acetaminophenLot = Lot(_acetaminophen, "A1", 100, new DateOnly(2027, 6, 30));
        var ibuprofenLot = Lot(_ibuprofen, "I1", 100, new DateOnly(2027, 6, 30));
        var sale = Sell(
            new SaleLine(_acetaminophen, _box.Id, 2, [acetaminophenLot]),
            new SaleLine(_ibuprofen, _ibuprofenBox.Id, 3, [ibuprofenLot]));

        var saleReturn = Return(sale, [Restock(sale.Items.First(), 1), Discard(sale.Items.Last(), 2)], [acetaminophenLot, ibuprofenLot]);

        Assert.Equal(12_000m + 2 * 8_000m, saleReturn.Refund);
    }

    // ---------- Validation ----------

    [Fact]
    public void Register_RejectsLinesOfAnotherTicket_ZeroQuantities_RepeatedLines_NoReason_NoLines_AndDatesBeforeTheSale()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));
        var sale = Sell(new SaleLine(_acetaminophen, _box.Id, 2, [batch]));
        var otherSale = Sell(new SaleLine(_acetaminophen, _box.Id, 1, [batch]));
        var sold = sale.Items.Single();

        Assert.Throws<DomainException>(() => Return(sale, [Restock(otherSale.Items.Single(), 1)], [batch]));
        Assert.Throws<DomainException>(() => Return(sale, [Restock(sold, 0)], [batch]));
        Assert.Throws<DomainException>(() => Return(sale, [Restock(sold, 1), Discard(sold, 1)], [batch]));
        Assert.Throws<DomainException>(() => Return(sale, [Restock(sold, 1)], [batch], reason: "  "));
        Assert.Throws<DomainException>(() => Return(sale, [], [batch]));
        Assert.Throws<DomainException>(() => Return(sale, [Restock(sold, 1)], [batch], at: SoldAt.AddMinutes(-1)));
        Assert.Throws<DomainException>(() => Return(sale, [new ReturnLine(sold.Id, 1, (ReturnDisposition)9)], [batch]));
        Assert.Throws<DomainException>(() => Return(sale, [Restock(sold, 1)], [batch], number: 0));
    }

    [Fact]
    public void Restock_RejectsNonPositiveQuantities()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));

        Assert.Throws<DomainException>(() => batch.Restock(0, ReturnedAt, Guid.CreateVersion7()));
    }
}
