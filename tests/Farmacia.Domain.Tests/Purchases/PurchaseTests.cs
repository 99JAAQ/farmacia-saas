using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Purchases;

namespace Farmacia.Domain.Tests.Purchases;

public class PurchaseTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid SupplierId = Guid.CreateVersion7();
    private static readonly Guid ProductId = Guid.CreateVersion7();
    private static readonly Guid BoxId = Guid.CreateVersion7();
    private static readonly DateOnly InvoiceDate = new(2026, 10, 20);
    private static readonly DateOnly FarExpiration = new(2027, 12, 31);
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-5));

    private static Purchase Draft() =>
        Purchase.CreateDraft(PharmacyId, SupplierId, " fe-1234 ", InvoiceDate, PaymentTerm.Cash);

    /// <summary>Boxes of 30 tablets.</summary>
    private static PurchaseItem AddBoxes(
        Purchase purchase, int quantity = 10, int bonus = 0, decimal unitCost = 9_000m, decimal discount = 0m,
        VatRate vat = VatRate.Exempt, string lot = "L1", Guid? productId = null, DateOnly? expiration = null) =>
        purchase.AddItem(productId ?? ProductId, BoxId, 30, quantity, bonus, unitCost, discount, vat, lot, expiration ?? FarExpiration);

    // ---------- Header ----------

    [Fact]
    public void CreateDraft_NormalizesTheInvoiceNumber_AndStartsAsDraft()
    {
        var purchase = Draft();

        Assert.Equal("FE-1234", purchase.InvoiceNumber);
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
        Assert.Empty(purchase.Items);
    }

    [Fact]
    public void Credit_RequiresADueDate_NotBeforeTheInvoiceDate()
    {
        Assert.Throws<DomainException>(() =>
            Purchase.CreateDraft(PharmacyId, SupplierId, "FE-1", InvoiceDate, PaymentTerm.Credit));
        Assert.Throws<DomainException>(() =>
            Purchase.CreateDraft(PharmacyId, SupplierId, "FE-1", InvoiceDate, PaymentTerm.Credit, InvoiceDate.AddDays(-1)));

        var credit = Purchase.CreateDraft(PharmacyId, SupplierId, "FE-1", InvoiceDate, PaymentTerm.Credit, InvoiceDate.AddDays(30));
        Assert.Equal(new DateOnly(2026, 11, 19), credit.DueDate);
    }

    [Fact]
    public void Cash_NeverKeepsADueDate()
    {
        var purchase = Purchase.CreateDraft(PharmacyId, SupplierId, "FE-1", InvoiceDate, PaymentTerm.Cash, InvoiceDate.AddDays(30));

        Assert.Null(purchase.DueDate);
    }

    // ---------- Lines ----------

    [Theory]
    [InlineData(0, 0, 9_000, 0)]       // nothing bought nor bonus
    [InlineData(-1, 0, 9_000, 0)]      // negative quantity
    [InlineData(10, 0, -1, 0)]         // negative cost
    [InlineData(10, 0, 9_000, 90_001)] // discount larger than the line
    public void AddItem_WithInvalidAmounts_Throws(int quantity, int bonus, decimal unitCost, decimal discount)
    {
        Assert.Throws<DomainException>(() => AddBoxes(Draft(), quantity, bonus, unitCost, discount));
    }

    [Fact]
    public void AddItem_SameProductAndLot_Twice_Throws_ButAnotherLotIsFine()
    {
        var purchase = Draft();
        AddBoxes(purchase, lot: "L1");

        Assert.Throws<DomainException>(() => AddBoxes(purchase, lot: "l1"));
        AddBoxes(purchase, lot: "L2");
        Assert.Equal(2, purchase.Items.Count);
    }

    [Fact]
    public void AddItem_WithAnUndefinedVatRate_Throws()
    {
        Assert.Throws<DomainException>(() => AddBoxes(Draft(), vat: (VatRate)7));
    }

    [Fact]
    public void RemoveItem_TakesTheLineOut()
    {
        var purchase = Draft();
        var item = AddBoxes(purchase);

        purchase.RemoveItem(item.Id);

        Assert.Empty(purchase.Items);
    }

    // ---------- Totals ----------

    [Fact]
    public void CalculateTotals_AddsVatOnlyToTheTaxedLines()
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 10, unitCost: 9_000m);                                     // medicine: 90.000, 0%
        purchase.AddItem(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 4, 0, 25_000m, 0m,     // cosmetic: 100.000, 19%
            VatRate.Standard, "C1", FarExpiration);

        var totals = purchase.CalculateTotals();

        Assert.Equal(190_000m, totals.Gross);
        Assert.Equal(190_000m, totals.TaxableBase);
        Assert.Equal(19_000m, totals.Vat);
        Assert.Equal(209_000m, totals.Total);
    }

    [Fact]
    public void InvoiceDiscount_IsSpreadInProportion_AndLowersTheVatBase()
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 6, unitCost: 10_000m, vat: VatRate.Standard);                        // 60.000
        AddBoxes(purchase, quantity: 4, unitCost: 10_000m, vat: VatRate.Standard, productId: Guid.CreateVersion7()); // 40.000
        purchase.SetInvoiceDiscount(10_000m);

        var totals = purchase.CalculateTotals();

        // Shares: 6.000 and 4.000, so the VAT base is 90.000, not 100.000.
        Assert.Equal(90_000m, totals.TaxableBase);
        Assert.Equal(17_100m, totals.Vat);
        Assert.Equal(107_100m, totals.Total);
    }

    [Fact]
    public void InvoiceDiscount_SharesAddUpToTheExactDiscount_DespiteRounding()
    {
        var purchase = Draft();
        for (var i = 0; i < 3; i++)
            AddBoxes(purchase, quantity: 1, unitCost: 10_000m, productId: Guid.CreateVersion7());
        purchase.SetInvoiceDiscount(100m); // 33,33 + 33,33 + 33,34

        Assert.Equal(29_900m, purchase.CalculateTotals().TaxableBase);
    }

    // ---------- Confirm ----------

    [Fact]
    public void Confirm_CreatesABatch_WithTheRealCostPerBaseUnit()
    {
        // 10 boxes x30 at 9.000, 2 bonus boxes, 3.000 line discount:
        // cost 87.000 for 360 tablets = 241,6667 each (not 300).
        var purchase = Draft();
        var item = AddBoxes(purchase, quantity: 10, bonus: 2, unitCost: 9_000m, discount: 3_000m);

        var batch = Assert.Single(purchase.Confirm(87_000m, vatIsPartOfCost: false, ReceivedAt));

        Assert.Equal(360, batch.Quantity);
        Assert.Equal(241.6667m, batch.UnitCost);
        Assert.Equal("L1", batch.LotNumber);
        Assert.Equal(ProductId, batch.ProductId);
        Assert.Equal(FarExpiration, batch.ExpirationDate);
        Assert.Equal(purchase.Id, Assert.Single(batch.PendingMovements).ReferenceId);

        Assert.Equal(PurchaseStatus.Confirmed, purchase.Status);
        Assert.Equal(ReceivedAt, purchase.ConfirmedAt);
        Assert.Equal(batch.Id, item.BatchId);
        Assert.Equal(241.6667m, item.UnitCostPerBaseUnit);
    }

    [Fact]
    public void Confirm_SpreadsTheInvoiceDiscountIntoEachBatchCost()
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 6, unitCost: 10_000m);                                     // 180 tablets
        AddBoxes(purchase, quantity: 4, unitCost: 10_000m, productId: Guid.CreateVersion7());   // 120 tablets
        purchase.SetInvoiceDiscount(10_000m);

        var batches = purchase.Confirm(90_000m, vatIsPartOfCost: false, ReceivedAt);

        // 54.000 / 180 and 36.000 / 120: both 300 per tablet.
        Assert.All(batches, b => Assert.Equal(300m, b.UnitCost));
    }

    [Theory]
    [InlineData(false, 300)] // VAT-responsible: purchase VAT is recovered, 90.000 / 300
    [InlineData(true, 357)]  // not responsible: VAT is a cost, 107.100 / 300
    public void Confirm_IncludesVatInTheCost_OnlyWhenItIsNotRecovered(bool vatIsPartOfCost, decimal expectedUnitCost)
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 10, unitCost: 9_000m, vat: VatRate.Standard);

        var batch = Assert.Single(purchase.Confirm(107_100m, vatIsPartOfCost, ReceivedAt));

        Assert.Equal(expectedUnitCost, batch.UnitCost);
    }

    [Fact]
    public void Confirm_ABonusOnlyLine_CreatesABatchWithZeroCost()
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 0, bonus: 2, unitCost: 9_000m);

        var batch = Assert.Single(purchase.Confirm(0m, vatIsPartOfCost: false, ReceivedAt));

        Assert.Equal(60, batch.Quantity);
        Assert.Equal(0m, batch.UnitCost);
    }

    [Theory]
    [InlineData(87_001)]
    [InlineData(86_999.5)]
    public void Confirm_AcceptsUpToOnePesoOfDifferenceWithThePrintedTotal(decimal printedTotal)
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 10, bonus: 2, discount: 3_000m);

        purchase.Confirm(printedTotal, vatIsPartOfCost: false, ReceivedAt);

        Assert.Equal(PurchaseStatus.Confirmed, purchase.Status);
    }

    [Fact]
    public void Confirm_WhenThePrintedTotalDoesNotMatch_Throws_ShowingTheDifference()
    {
        var purchase = Draft();
        AddBoxes(purchase, quantity: 10, bonus: 2, discount: 3_000m);

        var ex = Assert.Throws<DomainException>(() => purchase.Confirm(90_000m, vatIsPartOfCost: false, ReceivedAt));

        Assert.Contains("$90.000", ex.Message);
        Assert.Contains("$87.000", ex.Message);
        Assert.Contains("diferencia de $3.000", ex.Message);
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
    }

    [Fact]
    public void Confirm_WithAnExpiredLot_Throws_AndLeavesTheWholeDraftUntouched()
    {
        var purchase = Draft();
        var good = AddBoxes(purchase, lot: "BUENO");
        AddBoxes(purchase, lot: "VENCIDO", productId: Guid.CreateVersion7(), expiration: new DateOnly(2026, 10, 20));

        var ex = Assert.Throws<DomainException>(() => purchase.Confirm(180_000m, vatIsPartOfCost: false, ReceivedAt));

        Assert.Contains("VENCIDO", ex.Message);
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
        Assert.Null(good.BatchId); // the valid line was not half-received either
    }

    [Fact]
    public void Confirm_WithoutLines_Throws()
    {
        Assert.Throws<DomainException>(() => Draft().Confirm(0m, vatIsPartOfCost: false, ReceivedAt));
    }

    [Fact]
    public void Confirm_WithAnInvoiceDiscountLargerThanTheLines_Throws()
    {
        var purchase = Draft();
        AddBoxes(purchase); // 90.000
        purchase.SetInvoiceDiscount(100_000m);

        Assert.Throws<DomainException>(() => purchase.Confirm(-10_000m, vatIsPartOfCost: false, ReceivedAt));
    }

    [Fact]
    public void Confirm_AnInvoiceDatedAfterTheReception_Throws()
    {
        var purchase = Purchase.CreateDraft(PharmacyId, SupplierId, "FE-1", new DateOnly(2026, 10, 22), PaymentTerm.Cash);
        AddBoxes(purchase);

        Assert.Throws<DomainException>(() => purchase.Confirm(90_000m, vatIsPartOfCost: false, ReceivedAt));
    }

    [Fact]
    public void AConfirmedPurchase_CannotBeChanged()
    {
        var purchase = Draft();
        var item = AddBoxes(purchase);
        purchase.Confirm(90_000m, vatIsPartOfCost: false, ReceivedAt);

        Assert.Throws<DomainException>(() => AddBoxes(purchase, lot: "L2"));
        Assert.Throws<DomainException>(() => purchase.RemoveItem(item.Id));
        Assert.Throws<DomainException>(() => purchase.SetInvoiceDiscount(1_000m));
        Assert.Throws<DomainException>(() => purchase.UpdateHeader(SupplierId, "FE-2", InvoiceDate, PaymentTerm.Cash, null));
        Assert.Throws<DomainException>(() => purchase.Confirm(90_000m, vatIsPartOfCost: false, ReceivedAt));
    }
}
