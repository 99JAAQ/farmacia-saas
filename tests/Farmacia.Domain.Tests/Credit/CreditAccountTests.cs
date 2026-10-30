using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Tests.Credit;

public class CreditAccountTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid ShiftId = Guid.CreateVersion7();
    private static readonly Guid CashierId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Monday = new(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(-5));

    private static CreditAccount Account(decimal limit = 100_000m) => CreditAccount.Open(PharmacyId, CustomerId, limit);

    private static CreditPayment Pay(CreditAccount account, decimal amount, PaymentMethod method = PaymentMethod.Cash, long number = 1) =>
        CreditPayment.Register(number, account, ShiftId, CashierId, Monday.AddDays(3), method, amount);

    // ---------- Charging sales ----------

    [Fact]
    public void Charge_RaisesTheBalance_AndRecordsAnEntryWithTheBalanceAfter()
    {
        var account = Account();
        var saleId = Guid.CreateVersion7();

        account.Charge(30_000m, Monday, saleId);
        account.Charge(20_000m, Monday.AddDays(1), Guid.CreateVersion7());

        Assert.Equal((50_000m, 50_000m), (account.Balance, account.Available));
        var first = account.PendingEntries[0];
        Assert.Equal((CreditEntryType.Sale, 30_000m, 30_000m, saleId), (first.Type, first.Change, first.BalanceAfter, first.ReferenceId));
        Assert.Equal(50_000m, account.PendingEntries[1].BalanceAfter);
    }

    [Fact]
    public void Charge_OverTheLimit_IsRejected_AndSaysHowMuchIsAvailable()
    {
        var account = Account(limit: 50_000m);
        account.Charge(40_000m, Monday, Guid.CreateVersion7());

        var ex = Assert.Throws<DomainException>(() => account.Charge(10_001m, Monday, Guid.CreateVersion7()));

        Assert.Contains("10.000", ex.Message); // what is available
        Assert.Equal(40_000m, account.Balance);
        Assert.Single(account.PendingEntries);

        account.Charge(10_000m, Monday, Guid.CreateVersion7()); // exactly up to the limit is fine
        Assert.Equal(0m, account.Available);
    }

    [Fact]
    public void ASuspendedAccount_TakesNoSales_ButTakesPayments()
    {
        var account = Account();
        account.Charge(30_000m, Monday, Guid.CreateVersion7());

        account.Suspend();
        Assert.Throws<DomainException>(() => account.Charge(1_000m, Monday, Guid.CreateVersion7()));
        Pay(account, 10_000m);
        Assert.Equal(20_000m, account.Balance);

        account.Resume();
        account.Charge(1_000m, Monday, Guid.CreateVersion7());
        Assert.Equal(21_000m, account.Balance);
    }

    [Fact]
    public void ALimitBelowTheBalance_IsAllowed_ButStopsNewSales()
    {
        var account = Account(limit: 100_000m);
        account.Charge(80_000m, Monday, Guid.CreateVersion7());

        account.ChangeLimit(50_000m);

        Assert.Equal(0m, account.Available);
        Assert.Throws<DomainException>(() => account.Charge(1_000m, Monday, Guid.CreateVersion7()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Open_AndChangeLimit_NeedAPositiveLimit(decimal limit)
    {
        Assert.Throws<DomainException>(() => CreditAccount.Open(PharmacyId, CustomerId, limit));
        Assert.Throws<DomainException>(() => Account().ChangeLimit(limit));
        Assert.Throws<DomainException>(() => CreditAccount.Open(PharmacyId, Guid.Empty, 10_000m));
    }

    // ---------- Payments ("abonos") ----------

    [Fact]
    public void APayment_LowersTheBalance_WithItsOwnNumber_AndTellsWhatIsStillOwed()
    {
        var account = Account();
        account.Charge(50_000m, Monday, Guid.CreateVersion7());

        var payment = Pay(account, 20_000m, PaymentMethod.DigitalWallet, number: 7);

        Assert.Equal(30_000m, account.Balance);
        Assert.Equal((7, 20_000m, 30_000m, PaymentMethod.DigitalWallet), (payment.Number, payment.Amount, payment.BalanceAfter, payment.Method));
        Assert.Equal((account.Id, CustomerId, ShiftId), (payment.CreditAccountId, payment.CustomerId, payment.CashSessionId));
        var entry = account.PendingEntries.Last();
        Assert.Equal((CreditEntryType.Payment, -20_000m, payment.Id), (entry.Type, entry.Change, entry.ReferenceId));
    }

    [Fact]
    public void APayment_CannotGoOverTheDebt_NorBeZero_NorBePaidOnCredit()
    {
        var account = Account();
        account.Charge(50_000m, Monday, Guid.CreateVersion7());

        Assert.Throws<DomainException>(() => Pay(account, 50_001m));
        Assert.Throws<DomainException>(() => Pay(account, 0m));
        Assert.Throws<DomainException>(() => Pay(account, 10_000m, PaymentMethod.Credit));
        Assert.Throws<DomainException>(() => Pay(account, 10_000m, (PaymentMethod)99));
        Assert.Throws<DomainException>(() => Pay(account, 10_000m, number: 0));

        Assert.Equal(50_000m, account.Balance);
    }

    // ---------- How old the debt is ----------

    [Fact]
    public void DebtSince_StartsWithTheFirstDebt_KeepsWhileOwing_AndClearsWhenPaidOff()
    {
        var account = Account();
        Assert.Null(account.DebtSince);

        account.Charge(30_000m, Monday, Guid.CreateVersion7());
        account.Charge(10_000m, Monday.AddDays(5), Guid.CreateVersion7());
        Pay(account, 25_000m);
        Assert.Equal(Monday, account.DebtSince); // still owing since Monday

        Pay(account, 15_000m, number: 2);
        Assert.Null(account.DebtSince);

        account.Charge(5_000m, Monday.AddDays(9), Guid.CreateVersion7());
        Assert.Equal(Monday.AddDays(9), account.DebtSince); // a new debt starts its own count
    }

    // ---------- Returns of credit sales ----------

    [Fact]
    public void CreditReturn_LowersTheDebt_ButNeverBelowZero()
    {
        var account = Account();
        account.Charge(30_000m, Monday, Guid.CreateVersion7());
        var returnId = Guid.CreateVersion7();

        account.CreditReturn(12_000m, Monday.AddDays(1), returnId);

        Assert.Equal(18_000m, account.Balance);
        Assert.Equal((CreditEntryType.Return, -12_000m, returnId), (account.PendingEntries.Last().Type, account.PendingEntries.Last().Change, account.PendingEntries.Last().ReferenceId));
        Assert.Throws<DomainException>(() => account.CreditReturn(18_001m, Monday.AddDays(1), Guid.CreateVersion7()));
    }
}
