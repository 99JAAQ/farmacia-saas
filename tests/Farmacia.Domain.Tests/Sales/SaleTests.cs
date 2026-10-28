using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Tests.Sales;

public class SaleTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid CashSessionId = Guid.CreateVersion7();
    private static readonly Guid CashierId = Guid.CreateVersion7();
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset SoldAt = new(2026, 10, 28, 10, 0, 0, TimeSpan.FromHours(-5));

    // Acetaminofén: box of 30 at 12.000, tablet at 500; 0% VAT.
    private readonly Product _acetaminophen = Product.Create(PharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
    private readonly SaleUnit _box;
    private readonly SaleUnit _tablet;

    public SaleTests()
    {
        _box = _acetaminophen.AddSaleUnit("Caja x30", 30, 12_000m);
        _tablet = _acetaminophen.AddSaleUnit("Tableta", 1, 500m);
    }

    private static Batch Lot(Product product, string lot, int quantity, DateOnly expiration, decimal unitCost = 241.6667m) =>
        Batch.Receive(PharmacyId, product.Id, lot, expiration, quantity, unitCost, ReceivedAt);

    private static Sale Register(
        IReadOnlyList<SaleLine> lines, IReadOnlyList<PaymentLine>? payments = null, bool prescriptionChecked = false, long number = 1) =>
        Sale.Register(PharmacyId, number, CashSessionId, CashierId, SoldAt, lines, payments ?? [], prescriptionChecked);

    private static PaymentLine Cash(decimal received) => new(PaymentMethod.Cash, received);

    // ---------- Lines and prices ----------

    [Fact]
    public void Register_PricesTheLinesFromTheCatalog_AndCopiesWhatTheTicketNeeds()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));

        var sale = Register([new SaleLine(_acetaminophen, _box.Id, 2, [batch])], [Cash(24_000m)]);

        var item = Assert.Single(sale.Items);
        Assert.Equal(12_000m, item.UnitPrice);
        Assert.Equal(30, item.ConversionFactor);
        Assert.Equal(60, item.BaseUnits);
        Assert.Equal(24_000m, sale.Total);
        Assert.Equal(1, sale.Number);
        Assert.Equal(SoldAt, sale.SoldAt);
    }

    [Fact]
    public void Register_BreaksDownTheVatIncludedInThePrice()
    {
        var cream = Product.Create(PharmacyId, "Crema hidratante", "Tubo", false, vatRate: VatRate.Standard);
        var tube = cream.AddSaleUnit("Tubo", 1, 11_900m);

        var sale = Register([new SaleLine(cream, tube.Id, 1, [Lot(cream, "C1", 5, new DateOnly(2027, 1, 1))])], [Cash(11_900m)]);

        // 11.900 includes 19% VAT: 11.900 × 19 / 119 = 1.900. The price does not go up.
        Assert.Equal(11_900m, sale.Total);
        Assert.Equal(1_900m, sale.Vat);
    }

    [Fact]
    public void Register_WithoutLines_OrWithAZeroQuantity_OrAUnitOfAnotherProduct_Throws()
    {
        var batch = Lot(_acetaminophen, "L1", 100, new DateOnly(2027, 6, 30));
        var other = Product.Create(PharmacyId, "Ibuprofeno 400 mg", "Tableta", false);
        var otherBox = other.AddSaleUnit("Caja x10", 10, 8_000m);

        Assert.Throws<DomainException>(() => Register([], [Cash(1_000m)]));
        Assert.Throws<DomainException>(() => Register([new SaleLine(_acetaminophen, _box.Id, 0, [batch])], [Cash(1_000m)]));
        Assert.Throws<DomainException>(() => Register([new SaleLine(_acetaminophen, otherBox.Id, 1, [batch])], [Cash(8_000m)]));
    }

    // ---------- Stock (FEFO) ----------

    [Fact]
    public void Register_TakesTheStockFefo_AcrossBatches_AndRecordsTheCostOfEachOne()
    {
        var later = Lot(_acetaminophen, "TARDE", 100, new DateOnly(2027, 12, 31), unitCost: 250m);
        var sooner = Lot(_acetaminophen, "PRONTO", 20, new DateOnly(2027, 3, 31), unitCost: 200m);

        var sale = Register([new SaleLine(_acetaminophen, _box.Id, 1, [later, sooner])], [Cash(12_000m)]);

        // 30 tablets: the 20 that expire first, then 10 from the next batch.
        Assert.Equal(0, sooner.Quantity);
        Assert.Equal(90, later.Quantity);
        var item = Assert.Single(sale.Items);
        Assert.Equal([("PRONTO", 20), ("TARDE", 10)],
            item.Batches.Select(b => (b.BatchId == sooner.Id ? "PRONTO" : "TARDE", b.Quantity)).ToArray());
        Assert.Equal(6_500m, item.Cost); // 20 × 200 + 10 × 250

        // Each batch recorded its kardex movement, pointing to this sale.
        var movement = Assert.Single(sooner.PendingMovements, m => m.Type == StockMovementType.Sale);
        Assert.Equal(-20, movement.QuantityChange);
        Assert.Equal(sale.Id, movement.ReferenceId);
    }

    [Fact]
    public void Register_SkipsExpiredBatches()
    {
        var expired = Lot(_acetaminophen, "VENCIDO", 100, new DateOnly(2026, 10, 27)); // expired yesterday
        var good = Lot(_acetaminophen, "BUENO", 100, new DateOnly(2027, 6, 30));

        Register([new SaleLine(_acetaminophen, _tablet.Id, 10, [expired, good])], [Cash(5_000m)]);

        Assert.Equal(100, expired.Quantity);
        Assert.Equal(90, good.Quantity);
    }

    [Fact]
    public void Register_ChecksTheStockOfAProductForAllItsLines_BeforeTakingAnything()
    {
        // 32 tablets: a box (30) fits, and 5 loose tablets fit, but not both (35).
        var batch = Lot(_acetaminophen, "L1", 32, new DateOnly(2027, 6, 30));

        var error = Assert.Throws<DomainException>(() => Register(
            [new SaleLine(_acetaminophen, _box.Id, 1, [batch]), new SaleLine(_acetaminophen, _tablet.Id, 5, [batch])],
            [Cash(15_000m)]));

        Assert.Contains("Acetaminofén", error.Message);
        Assert.Equal(32, batch.Quantity);          // the first line did not take its 30
        Assert.Single(batch.PendingMovements);     // only the reception
    }

    // ---------- Prescription ----------

    [Fact]
    public void Register_AProductThatNeedsAPrescription_RequiresTheCheck_AndRecordsWhoAndWhen()
    {
        var antibiotic = Product.Create(PharmacyId, "Amoxicilina 500 mg", "Cápsula", requiresPrescription: true);
        var box = antibiotic.AddSaleUnit("Caja x15", 15, 18_000m);
        SaleLine Line() => new(antibiotic, box.Id, 1, [Lot(antibiotic, "A1", 30, new DateOnly(2027, 6, 30))]);

        Assert.Throws<DomainException>(() => Register([Line()], [Cash(18_000m)], prescriptionChecked: false));

        var sale = Register([Line()], [Cash(18_000m)], prescriptionChecked: true);
        Assert.Equal(CashierId, sale.PrescriptionCheckedBy);
        Assert.Equal(SoldAt, sale.PrescriptionCheckedAt);
        Assert.True(Assert.Single(sale.Items).RequiresPrescription);
    }

    [Fact]
    public void Register_WithoutPrescriptionProducts_DoesNotRecordACheck()
    {
        var sale = Register(
            [new SaleLine(_acetaminophen, _tablet.Id, 1, [Lot(_acetaminophen, "L1", 10, new DateOnly(2027, 6, 30))])],
            [Cash(500m)], prescriptionChecked: true);

        Assert.Null(sale.PrescriptionCheckedBy);
    }

    // ---------- Payments ----------

    private Sale RegisterBoxes(int boxes, params PaymentLine[] payments) =>
        Register([new SaleLine(_acetaminophen, _box.Id, boxes, [Lot(_acetaminophen, "L1", 300, new DateOnly(2027, 6, 30))])], payments);

    [Fact]
    public void Cash_GivesChange_AndOnlyTheAmountDueCountsAsPaid()
    {
        var sale = RegisterBoxes(1, Cash(50_000m));

        var payment = Assert.Single(sale.Payments);
        Assert.Equal(12_000m, payment.Amount);
        Assert.Equal(50_000m, payment.Received);
        Assert.Equal(38_000m, payment.Change);
        Assert.Equal(38_000m, sale.Change);
    }

    [Fact]
    public void MixedPayment_CardPaysItsAmount_AndCashCoversTheRest()
    {
        var sale = RegisterBoxes(3, new PaymentLine(PaymentMethod.Card, 20_000m), Cash(20_000m));

        // 36.000: 20.000 by card, 16.000 in cash, 4.000 of change.
        Assert.Equal(20_000m, sale.Payments.Single(p => p.Method == PaymentMethod.Card).Amount);
        var cash = sale.Payments.Single(p => p.Method == PaymentMethod.Cash);
        Assert.Equal(16_000m, cash.Amount);
        Assert.Equal(4_000m, cash.Change);
        Assert.Equal(sale.Total, sale.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public void ExactNonCashPayments_AreAccepted_WithoutChange()
    {
        var sale = RegisterBoxes(2,
            new PaymentLine(PaymentMethod.DigitalWallet, 10_000m), new PaymentLine(PaymentMethod.BankTransfer, 14_000m));

        Assert.Equal(0m, sale.Change);
        Assert.All(sale.Payments, p => Assert.Null(p.Received));
    }

    public static TheoryData<PaymentLine[]> InvalidPayments => new()
    {
        { [] },                                                                          // nothing paid
        { [new PaymentLine(PaymentMethod.Cash, 11_999m)] },                              // cash short by 1
        { [new PaymentLine(PaymentMethod.Card, 11_000m)] },                              // card short, no cash
        { [new PaymentLine(PaymentMethod.Card, 13_000m)] },                              // card over the total
        { [new PaymentLine(PaymentMethod.Card, 12_000m), new PaymentLine(PaymentMethod.Cash, 1_000m)] }, // cash not needed
        { [new PaymentLine(PaymentMethod.Cash, 6_000m), new PaymentLine(PaymentMethod.Cash, 6_000m)] },  // two cash payments
        { [new PaymentLine(PaymentMethod.Card, 0m), new PaymentLine(PaymentMethod.Cash, 12_000m)] },     // zero payment
        { [new PaymentLine((PaymentMethod)99, 12_000m)] },                               // unknown method
    };

    [Theory]
    [MemberData(nameof(InvalidPayments))]
    public void InvalidPayments_Throw_AndLeaveTheStockUntouched(PaymentLine[] payments)
    {
        var batch = Lot(_acetaminophen, "L1", 300, new DateOnly(2027, 6, 30));

        Assert.Throws<DomainException>(() => Register([new SaleLine(_acetaminophen, _box.Id, 1, [batch])], payments));
        Assert.Equal(300, batch.Quantity);
    }
}
