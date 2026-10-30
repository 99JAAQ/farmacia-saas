using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Tests.Credit;

/// <summary>Credit inside a sale and inside a return: the limit before any stock moves, the debt before any cash.</summary>
public class CreditSaleTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid ShiftId = Guid.CreateVersion7();
    private static readonly Guid CashierId = Guid.CreateVersion7();
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset SoldAt = new(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(-5));

    // Box of 30 at 12.000: 2 boxes = 24.000.
    private readonly Product _acetaminophen = Product.Create(PharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
    private readonly SaleUnit _box;
    private readonly Batch _batch;
    private readonly CreditAccount _account = CreditAccount.Open(PharmacyId, Guid.CreateVersion7(), limit: 50_000m);

    public CreditSaleTests()
    {
        _box = _acetaminophen.AddSaleUnit("Caja x30", 30, 12_000m);
        _batch = Batch.Receive(PharmacyId, _acetaminophen.Id, "L1", new DateOnly(2027, 6, 30), 300, 250m, ReceivedAt);
    }

    private Sale Sell(int boxes, params PaymentLine[] payments) => SellWith(_account, boxes, payments);

    private Sale SellWith(CreditAccount? account, int boxes, params PaymentLine[] payments) =>
        Sale.Register(PharmacyId, 1, ShiftId, CashierId, SoldAt,
            [new SaleLine(_acetaminophen, _box.Id, boxes, [_batch])], payments, false, account);

    private SaleReturn Return(Sale sale, int boxes, IReadOnlyList<SaleReturn>? previous = null, CreditAccount? account = null) =>
        SaleReturn.Register(previous is null ? 1 : previous.Count + 1, sale, previous ?? [], ShiftId, CashierId, SoldAt.AddDays(3),
            "El cliente se equivocó", [new ReturnLine(sale.Items.Single().Id, boxes, ReturnDisposition.Restock)], [_batch],
            account ?? _account);

    private static PaymentLine Cash(decimal amount) => new(PaymentMethod.Cash, amount);

    private static PaymentLine OnCredit(decimal amount) => new(PaymentMethod.Credit, amount);

    // ---------- Sales ----------

    [Fact]
    public void AMixedSale_ChargesTheCreditPartToTheAccount_AndKeepsTheCustomer()
    {
        var sale = Sell(2, Cash(10_000m), OnCredit(14_000m));

        Assert.Equal((14_000m, _account.CustomerId), (sale.CreditAmount, sale.CustomerId));
        Assert.Equal(14_000m, _account.Balance);
        var entry = Assert.Single(_account.PendingEntries);
        Assert.Equal((CreditEntryType.Sale, 14_000m, sale.Id), (entry.Type, entry.Change, entry.ReferenceId));
        Assert.Equal(0m, sale.Change); // credit gives no change; the cash was exact
    }

    [Fact]
    public void ACreditOverTheLimit_LeavesTheStockAndTheAccountUntouched()
    {
        Assert.Throws<DomainException>(() => Sell(5, OnCredit(60_000m))); // limit 50.000

        Assert.Equal(300, _batch.Quantity);
        Assert.Equal(0m, _account.Balance);
        Assert.Empty(_account.PendingEntries);
    }

    [Fact]
    public void ASuspendedAccount_CannotTakeASale()
    {
        _account.Suspend();

        Assert.Throws<DomainException>(() => Sell(1, OnCredit(12_000m)));
        Assert.Equal(300, _batch.Quantity);
    }

    [Fact]
    public void Credit_PaysAnExactAmount_InASinglePayment_NeverOverTheTotal_AndNeedsTheAccount()
    {
        Assert.Throws<DomainException>(() => Sell(2, OnCredit(10_000m), OnCredit(14_000m)));
        Assert.Throws<DomainException>(() => Sell(2, OnCredit(25_000m)));
        Assert.Throws<DomainException>(() => Sell(2, OnCredit(20_000m))); // 4.000 left unpaid
        Assert.Throws<DomainException>(() => SellWith(null, 2, OnCredit(24_000m)));

        var otherPharmacy = CreditAccount.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), 50_000m);
        Assert.Throws<ArgumentException>(() => SellWith(otherPharmacy, 2, OnCredit(24_000m)));

        Assert.Equal(0m, _account.Balance);
    }

    [Fact]
    public void ASaleWithoutCredit_ChargesNothing_EvenWithTheCustomersAccount()
    {
        var sale = Sell(1, Cash(12_000m));

        Assert.Equal(_account.CustomerId, sale.CustomerId);
        Assert.Equal(0m, _account.Balance);
    }

    // ---------- Returns ----------

    [Fact]
    public void ReturningPartOfACreditSale_LowersTheDebt_AndGivesNoCash()
    {
        var sale = Sell(2, OnCredit(24_000m));

        var saleReturn = Return(sale, 1);

        Assert.Equal((12_000m, 12_000m, 0m), (saleReturn.Refund, saleReturn.CreditedToAccount, saleReturn.CashRefund));
        Assert.Equal(12_000m, _account.Balance);
        Assert.Equal((CreditEntryType.Return, -12_000m, saleReturn.Id), (_account.PendingEntries.Last().Type, _account.PendingEntries.Last().Change, _account.PendingEntries.Last().ReferenceId));
    }

    [Fact]
    public void ReturningAMixedSale_LowersTheDebtFirst_AndGivesTheRestInCash()
    {
        var sale = Sell(2, Cash(10_000m), OnCredit(14_000m));

        var saleReturn = Return(sale, 2);

        Assert.Equal((14_000m, 10_000m), (saleReturn.CreditedToAccount, saleReturn.CashRefund));
        Assert.Equal(0m, _account.Balance);
    }

    [Fact]
    public void TwoReturnsOfOneSale_NeverLowerTheDebtByMoreThanWasOnCredit()
    {
        var sale = Sell(2, Cash(10_000m), OnCredit(14_000m));
        _account.Charge(30_000m, SoldAt, Guid.CreateVersion7()); // another sale on credit: the debt is 44.000

        var first = Return(sale, 1);
        var second = Return(sale, 1, previous: [first]);

        Assert.Equal((12_000m, 0m), (first.CreditedToAccount, first.CashRefund));
        Assert.Equal((2_000m, 10_000m), (second.CreditedToAccount, second.CashRefund)); // only 2.000 of this ticket were left on credit
        Assert.Equal(30_000m, _account.Balance); // the other sale's debt is untouched
    }

    [Fact]
    public void IfTheDebtWasAlreadyPaid_TheReturnGivesCash()
    {
        var sale = Sell(2, OnCredit(24_000m));
        CreditPayment.Register(1, _account, ShiftId, CashierId, SoldAt.AddDays(1), PaymentMethod.Cash, 20_000m); // owes 4.000

        var saleReturn = Return(sale, 1);

        Assert.Equal((4_000m, 8_000m), (saleReturn.CreditedToAccount, saleReturn.CashRefund));
        Assert.Equal(0m, _account.Balance); // never below zero
    }

    [Fact]
    public void ReturningACreditSale_NeedsItsCustomersAccount()
    {
        var sale = Sell(2, OnCredit(24_000m));
        var someoneElse = CreditAccount.Open(PharmacyId, Guid.CreateVersion7(), 50_000m);

        Assert.Throws<ArgumentException>(() => Return(sale, 1, account: someoneElse));
        Assert.Equal(24_000m, _account.Balance);
    }
}
