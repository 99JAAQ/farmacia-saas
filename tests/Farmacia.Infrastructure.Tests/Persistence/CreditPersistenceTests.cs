using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Cash.CloseCashSession;
using Farmacia.Application.Cash.OpenCashSession;
using Farmacia.Application.Credit.RegisterCreditPayment;
using Farmacia.Application.SaleReturns.RegisterSaleReturn;
using Farmacia.Application.Sales;
using Farmacia.Application.Sales.RegisterSale;
using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;
using Farmacia.Infrastructure.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Persistence;

/// <summary>
/// Credit against a real SQL Server: the account and its entries in the sale's transaction, payments with their own
/// numbers, a shift whose cash mixes sales, payments and a credit return, and the limit under concurrency.
/// Each test uses its own pharmacy.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CreditPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private Guid _pharmacyId;
    private ServiceProvider _services = null!;
    private Guid _shiftId;
    private Guid _customerId;
    private Guid _productId;
    private Guid _boxId;

    public async Task InitializeAsync()
    {
        var pharmacy = Pharmacy.Create("Droguería del Fiado", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
        var cashier = new ApplicationUser { FullName = "Ana Cajera", PharmacyId = pharmacy.Id };
        var customer = Customer.Create(pharmacy.Id, "Doña Rosa", DocumentType.CitizenshipCard, "41234567");
        var account = CreditAccount.Open(pharmacy.Id, customer.Id, limit: 30_000m);
        var product = Product.Create(pharmacy.Id, "Acetaminofén 500 mg", "Tableta", false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        var batch = Batch.Receive(pharmacy.Id, product.Id, "L1", new DateOnly(2030, 12, 31), 300, 250m,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, SqlServerFixture.Colombia));

        await using (var db = fixture.CreateContext())
        {
            db.Pharmacies.Add(pharmacy);
            db.Users.Add(cashier);
            db.AddRange(customer, account, product, batch);
            await db.SaveChangesAsync();
        }

        (_pharmacyId, _customerId, _productId, _boxId) = (pharmacy.Id, customer.Id, product.Id, box.Id);
        _services = InfrastructureServices.Build(fixture, pharmacyId: _pharmacyId, currentUser: new FixedUser(cashier.Id));
        _shiftId = await SendAsync(new OpenCashSessionCommand(100_000m));
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private Task<SaleDto> SellAsync(int boxes, params RegisterSalePayment[] payments) =>
        SendAsync(new RegisterSaleCommand([new RegisterSaleLine(_productId, _boxId, boxes)], payments, false, _customerId));

    private static RegisterSalePayment Cash(decimal amount) => new(PaymentMethod.Cash, amount);

    private static RegisterSalePayment OnCredit(decimal amount) => new(PaymentMethod.Credit, amount);

    [Fact]
    public async Task ACreditSaleAndAPayment_SaveTheBalanceAndItsEntries_AndPaymentsHaveTheirOwnNumbers()
    {
        var ticket = await SellAsync(2, Cash(10_000m), OnCredit(14_000m));
        var voucher = await SendAsync(new RegisterCreditPaymentCommand(_customerId, PaymentMethod.Cash, 4_000m));

        await using var db = fixture.CreateContext();
        var account = await db.CreditAccounts.SingleAsync(a => a.CustomerId == _customerId);
        Assert.Equal(10_000m, account.Balance);
        var entries = await db.CreditEntries.Where(e => e.CreditAccountId == account.Id).OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToListAsync();
        Assert.Equal(
            [(CreditEntryType.Sale, 14_000m, 14_000m, ticket.Id), (CreditEntryType.Payment, -4_000m, 10_000m, voucher.Id)],
            entries.Select(e => (e.Type, e.Change, e.BalanceAfter, e.ReferenceId)));

        Assert.Equal(_customerId, (await db.Sales.SingleAsync(s => s.Id == ticket.Id)).CustomerId);
        Assert.Equal((1, 1), (ticket.Number, voucher.Number)); // tickets and payments count separately
    }

    [Fact]
    public async Task ClosingTheShift_CountsSales_CashPayments_AndOnlyTheCashPartOfACreditReturn()
    {
        var ticket = await SellAsync(2, Cash(10_000m), OnCredit(14_000m));                      // +10.000 cash, owes 14.000
        await SendAsync(new RegisterCreditPaymentCommand(_customerId, PaymentMethod.Cash, 5_000m));         // +5.000 cash, owes 9.000
        await SendAsync(new RegisterCreditPaymentCommand(_customerId, PaymentMethod.DigitalWallet, 1_000m)); // Nequi: not in the drawer, owes 8.000

        // 1 box (12.000): the debt of this ticket goes down first (8.000 owed), the other 4.000 in cash.
        var voucher = await SendAsync(new RegisterSaleReturnCommand(ticket.Id, "No le sirvió",
            [new RegisterSaleReturnLine(ticket.Items.Single().Id, 1, ReturnDisposition.Restock)]));
        Assert.Equal((8_000m, 4_000m), (voucher.CreditedToAccount, voucher.CashRefund));

        var closed = await SendAsync(new CloseCashSessionCommand(_shiftId, 111_000m, null));

        Assert.Equal(111_000m, closed.ExpectedCash); // 100.000 + 10.000 + 5.000 − 4.000
        await using var db = fixture.CreateContext();
        Assert.Equal(0m, (await db.CreditAccounts.SingleAsync(a => a.CustomerId == _customerId)).Balance);
    }

    [Fact]
    public async Task TwoSimultaneousCreditSales_NeverPassTheLimit()
    {
        // Each sale takes from a batch of its own, so only the credit account is shared: otherwise the batch's
        // RowVersion would stop the second sale and the test would pass even without the account's.
        var (otherProductId, otherBoxId) = await SeedOtherProductAsync();

        // Limit 30.000: each sale alone fits (24.000), both together would not.
        var results = await Task.WhenAll(
            Attempt(() => SellAsync(2, OnCredit(24_000m))),
            Attempt(() => SendAsync(new RegisterSaleCommand(
                [new RegisterSaleLine(otherProductId, otherBoxId, 2)], [OnCredit(24_000m)], false, _customerId))));

        Assert.Single(results, succeeded => succeeded);
        await using var db = fixture.CreateContext();
        Assert.Equal(24_000m, (await db.CreditAccounts.SingleAsync(a => a.CustomerId == _customerId)).Balance);
        Assert.Equal(1, await db.Sales.CountAsync(s => s.PharmacyId == _pharmacyId));
    }

    private async Task<(Guid ProductId, Guid BoxId)> SeedOtherProductAsync()
    {
        var product = Product.Create(_pharmacyId, "Ibuprofeno 400 mg", "Tableta", false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        var batch = Batch.Receive(_pharmacyId, product.Id, "I1", new DateOnly(2030, 12, 31), 300, 250m,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, SqlServerFixture.Colombia));
        await using var db = fixture.CreateContext();
        db.AddRange(product, batch);
        await db.SaveChangesAsync();
        return (product.Id, box.Id);
    }

    /// <summary>The second sale either finds the limit used up (400) or collides on the account's RowVersion (409).</summary>
    private static async Task<bool> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is DomainException or Application.Common.Exceptions.ConflictException)
        {
            return false;
        }
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;

        public bool IsInRole(string role) => role == Roles.Cashier;
    }
}
