using Farmacia.Domain.Common;
using Farmacia.Domain.Purchases;

namespace Farmacia.Domain.Tests.Purchases;

/// <summary>Paying a credit invoice: per invoice, never over what is owed, outside the register.</summary>
public class SupplierPaymentTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly DateOnly InvoiceDate = new(2026, 10, 20);
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset PaidAt = new(2026, 11, 5, 15, 0, 0, TimeSpan.FromHours(-5));

    /// <summary>10 boxes at 9.000 = 90.000, confirmed.</summary>
    private static Purchase Confirmed(PaymentTerm term = PaymentTerm.Credit)
    {
        var purchase = Purchase.CreateDraft(PharmacyId, Guid.CreateVersion7(), "FE-77", InvoiceDate, term,
            term == PaymentTerm.Credit ? new DateOnly(2026, 11, 19) : null);
        purchase.AddItem(Guid.CreateVersion7(), Guid.CreateVersion7(), 30, 10, 0, 9_000m, 0m, VatRate.Exempt, "L1", new DateOnly(2028, 1, 31));
        purchase.Confirm(90_000m, vatIsPartOfCost: false, ReceivedAt);
        return purchase;
    }

    private static SupplierPayment Pay(Purchase purchase, decimal amount, SupplierPaymentMethod method = SupplierPaymentMethod.BankTransfer, string? reference = "TRX-1") =>
        purchase.RegisterPayment(amount, method, reference, PaidAt, Owner);

    [Fact]
    public void ACreditInvoice_IsOwedInFull_AndEachPaymentLowersItsBalance()
    {
        var purchase = Confirmed();
        Assert.Equal((true, 90_000m), (purchase.IsPayable, purchase.Balance));

        var payment = Pay(purchase, 40_000m, reference: " TRX-981 ");
        Pay(purchase, 50_000m, SupplierPaymentMethod.Cash, reference: null); // from the safe: no number needed

        Assert.Equal((90_000m, 0m), (purchase.AmountPaid, purchase.Balance));
        Assert.Equal(("TRX-981", Owner, purchase.Id), (payment.Reference, payment.PaidBy, payment.PurchaseId));
    }

    [Fact]
    public void APayment_NeverGoesOverWhatIsOwed()
    {
        var purchase = Confirmed();
        Pay(purchase, 60_000m);

        var ex = Assert.Throws<DomainException>(() => Pay(purchase, 30_001m));

        Assert.Contains("30.000", ex.Message); // what is left
        Assert.Equal(30_000m, purchase.Balance);
    }

    [Fact]
    public void ACashPurchase_OrADraft_HasNothingToPay()
    {
        Assert.Equal((false, 0m), (Confirmed(PaymentTerm.Cash).IsPayable, Confirmed(PaymentTerm.Cash).Balance));
        Assert.Contains("contado", Assert.Throws<DomainException>(() => Pay(Confirmed(PaymentTerm.Cash), 1_000m)).Message);

        var draft = Purchase.CreateDraft(PharmacyId, Guid.CreateVersion7(), "FE-78", InvoiceDate, PaymentTerm.Credit, new DateOnly(2026, 11, 19));
        Assert.Contains("borrador", Assert.Throws<DomainException>(() => Pay(draft, 1_000m)).Message);
    }

    [Fact]
    public void ATransferOrACheck_CarryTheirNumber_AndAPaymentNeedsAPositiveAmountADateAndWhoPaid()
    {
        var purchase = Confirmed();

        Assert.Throws<DomainException>(() => Pay(purchase, 1_000m, SupplierPaymentMethod.BankTransfer, reference: " "));
        Assert.Throws<DomainException>(() => Pay(purchase, 1_000m, SupplierPaymentMethod.Check, reference: null));
        Assert.Throws<DomainException>(() => Pay(purchase, 0m));
        Assert.Throws<DomainException>(() => Pay(purchase, 1_000m, (SupplierPaymentMethod)9));
        Assert.Throws<DomainException>(() => purchase.RegisterPayment(1_000m, SupplierPaymentMethod.Cash, null, ReceivedAt.AddDays(-5), Owner));
        Assert.Throws<DomainException>(() => purchase.RegisterPayment(1_000m, SupplierPaymentMethod.Cash, null, PaidAt, Guid.Empty));

        Assert.Equal(90_000m, purchase.Balance);
    }

    [Fact]
    public void AnInvoiceIsOverdue_AfterItsDueDate_WhileSomethingIsOwed()
    {
        var purchase = Confirmed(); // due 19 Nov 2026

        Assert.False(purchase.IsOverdueOn(new DateOnly(2026, 11, 19))); // the due date itself is still on time
        Assert.True(purchase.IsOverdueOn(new DateOnly(2026, 11, 20)));

        Pay(purchase, 90_000m);
        Assert.False(purchase.IsOverdueOn(new DateOnly(2026, 11, 20)));
    }
}
