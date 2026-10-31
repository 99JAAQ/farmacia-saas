using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Credit.ChangeCreditLimit;
using Farmacia.Application.Credit.GetCreditStatement;
using Farmacia.Application.Credit.ListDebtors;
using Farmacia.Application.Credit.OpenCreditAccount;
using Farmacia.Application.Credit.SetCreditSuspended;
using Farmacia.Application.Customers.CreateCustomer;
using Farmacia.Application.Customers.SearchCustomers;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;

namespace Farmacia.Application.Tests.Credit;

/// <summary>The owner's side of credit: customers, accounts, limits, statement, debtors.</summary>
public class CreditAdministrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryCreditAccountRepository _accounts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentPharmacy _pharmacy;
    private readonly FakeClock _clock = new(Now);

    public CreditAdministrationTests() => _pharmacy = new FakeCurrentPharmacy(_pharmacyId);

    private Customer AddCustomer(string name, string document)
    {
        var customer = Customer.Create(_pharmacyId, name, DocumentType.CitizenshipCard, document);
        _customers.Add(customer);
        return customer;
    }

    private CreditAccount AddAccount(Customer customer, decimal limit = 50_000m)
    {
        var account = CreditAccount.Open(_pharmacyId, customer.Id, limit);
        _accounts.Add(account);
        return account;
    }

    [Fact]
    public async Task CreateCustomer_WithARepeatedDocument_IsAConflict()
    {
        var handler = new CreateCustomerHandler(_customers, _unitOfWork, _pharmacy);
        var command = new CreateCustomerCommand("Doña Rosa", DocumentType.CitizenshipCard, "41234567", "3001234567", null);

        var id = await handler.Handle(command, CancellationToken.None);
        Assert.Equal("Doña Rosa", Assert.Single(_customers.Items, c => c.Id == id).FullName);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command with { FullName = "Rosa" }, CancellationToken.None));
    }

    [Fact]
    public async Task OpenCreditAccount_ForAnUnknownCustomer_IsNotFound_AndTwice_IsAConflict()
    {
        var rosa = AddCustomer("Doña Rosa", "41234567");
        var handler = new OpenCreditAccountHandler(_customers, _accounts, _unitOfWork, _pharmacy);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new OpenCreditAccountCommand(Guid.CreateVersion7(), 50_000m), CancellationToken.None));

        await handler.Handle(new OpenCreditAccountCommand(rosa.Id, 50_000m), CancellationToken.None);
        Assert.Equal(50_000m, Assert.Single(_accounts.Items).Limit);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new OpenCreditAccountCommand(rosa.Id, 80_000m), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeLimit_AndSuspend_ChangeTheAccount_AndNeedOne()
    {
        var rosa = AddCustomer("Doña Rosa", "41234567");
        var account = AddAccount(rosa);

        await new ChangeCreditLimitHandler(_accounts, _unitOfWork, _pharmacy).Handle(new ChangeCreditLimitCommand(rosa.Id, 80_000m), CancellationToken.None);
        await new SetCreditSuspendedHandler(_accounts, _unitOfWork, _pharmacy).Handle(new SetCreditSuspendedCommand(rosa.Id, true), CancellationToken.None);

        Assert.Equal((80_000m, true), (account.Limit, account.IsSuspended));
        await Assert.ThrowsAsync<DomainException>(() =>
            new ChangeCreditLimitHandler(_accounts, _unitOfWork, _pharmacy).Handle(new ChangeCreditLimitCommand(rosa.Id, 0m), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SetCreditSuspendedHandler(_accounts, _unitOfWork, _pharmacy).Handle(new SetCreditSuspendedCommand(Guid.CreateVersion7(), true), CancellationToken.None));
    }

    [Fact]
    public async Task Search_ShowsEachCustomersCredit_OrNullWithoutAnAccount()
    {
        var rosa = AddCustomer("Doña Rosa", "41234567");
        AddCustomer("Don Pedro", "79876543");
        AddAccount(rosa).Charge(20_000m, Now.AddDays(-3), Guid.CreateVersion7());

        var found = await new SearchCustomersHandler(_customers, _accounts, _pharmacy, _clock).Handle(new SearchCustomersQuery(null), CancellationToken.None);

        Assert.Equal(["Don Pedro", "Doña Rosa"], found.Select(c => c.FullName));
        Assert.Null(found[0].Credit);
        Assert.Equal((20_000m, 30_000m, 3), (found[1].Credit!.Balance, found[1].Credit!.Available, found[1].Credit!.DaysOwing));
    }

    [Fact]
    public async Task Statement_ShowsTheBalance_AndTheMovementsNewestFirst()
    {
        var rosa = AddCustomer("Doña Rosa", "41234567");
        var account = AddAccount(rosa);
        var saleId = Guid.CreateVersion7();
        account.Charge(20_000m, Now.AddDays(-2), saleId);
        CreditPayment.Register(1, account, Guid.CreateVersion7(), Guid.CreateVersion7(), Now.AddDays(-1), Domain.Sales.PaymentMethod.Cash, 5_000m);

        var statement = await new GetCreditStatementHandler(_customers, _accounts, _pharmacy, _clock)
            .Handle(new GetCreditStatementQuery(rosa.Id), CancellationToken.None);

        Assert.Equal(("Doña Rosa", 15_000m), (statement.CustomerName, statement.Account.Balance));
        Assert.Equal([CreditEntryType.Payment, CreditEntryType.Sale], statement.Entries.Select(e => e.Type));
        Assert.Equal(saleId, statement.Entries[1].ReferenceId);
    }

    [Fact]
    public async Task Debtors_AreOnlyThoseWhoOwe_TheOldestDebtFirst()
    {
        var rosa = AddCustomer("Doña Rosa", "41234567");
        var pedro = AddCustomer("Don Pedro", "79876543");
        var lucia = AddCustomer("Lucía", "52111222");
        AddAccount(rosa).Charge(10_000m, Now.AddDays(-2), Guid.CreateVersion7());
        AddAccount(pedro).Charge(30_000m, Now.AddDays(-20), Guid.CreateVersion7());
        AddAccount(lucia); // owes nothing

        var debtors = await new ListDebtorsHandler(_customers, _accounts, _pharmacy, _clock).Handle(new ListDebtorsQuery(), CancellationToken.None);

        Assert.Equal([("Don Pedro", 20), ("Doña Rosa", 2)], debtors.Select(d => (d.CustomerName, d.Account.DaysOwing!.Value)));
    }
}
