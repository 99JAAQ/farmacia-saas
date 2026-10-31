using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Credit;
using Farmacia.Application.Credit.GetCreditPaymentById;
using Farmacia.Application.Credit.RegisterCreditPayment;
using Farmacia.Application.SaleReturns;
using Farmacia.Application.SaleReturns.RegisterSaleReturn;
using Farmacia.Application.Sales;
using Farmacia.Application.Sales.RegisterSale;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Cash;
using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Tests.Credit;

/// <summary>Credit through the use cases: a sale on credit, an "abono", a return of a credit sale.</summary>
public class CreditHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly Guid _ana = Guid.CreateVersion7();
    private readonly Guid _beto = Guid.CreateVersion7();

    private readonly InMemoryCashSessionRepository _sessions = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryBatchRepository _batches = new();
    private readonly InMemorySaleRepository _sales = new();
    private readonly InMemorySaleReturnRepository _returns = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryCreditAccountRepository _creditAccounts = new();
    private readonly InMemoryCreditPaymentRepository _payments = new();
    private readonly FakeDocumentNumberSequence _documentNumbers = new();
    private readonly FakeUserReader _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Product _product;
    private readonly SaleUnit _box;
    private readonly Customer _customer;
    private readonly CreditAccount _account;

    public CreditHandlersTests()
    {
        _users.Users[_ana] = (_pharmacyId, "Ana Cajera");
        _product = Product.Create(_pharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
        _box = _product.AddSaleUnit("Caja x30", 30, 12_000m);
        _products.Add(_product);
        _batches.Add(Batch.Receive(_pharmacyId, _product.Id, "L1", new DateOnly(2027, 6, 30), 300, 250m, Now.AddDays(-10)));
        _sessions.Add(CashSession.Open(_pharmacyId, _ana, 100_000m, Now.AddHours(-2)));

        _customer = Customer.Create(_pharmacyId, "Doña Rosa", DocumentType.CitizenshipCard, "41234567");
        _customers.Add(_customer);
        _account = CreditAccount.Open(_pharmacyId, _customer.Id, limit: 50_000m);
        _creditAccounts.Add(_account);
    }

    private FakeCurrentUser Cashier(Guid userId) => new(userId, Roles.Cashier);

    private Task<SaleDto> SellAsync(int boxes, IReadOnlyList<RegisterSalePayment> payments, Guid? customerId) =>
        new RegisterSaleHandler(
                _sessions, _products, _batches, _sales, _creditAccounts, _documentNumbers, _unitOfWork,
                new FakeCurrentPharmacy(_pharmacyId), Cashier(_ana), new FakeClock(Now),
                new SaleReader(_products, _users, _returns, _customers))
            .Handle(new RegisterSaleCommand([new RegisterSaleLine(_product.Id, _box.Id, boxes)], payments, false, customerId),
                CancellationToken.None);

    private Task<CreditPaymentDto> PayAsync(Guid userId, decimal amount, PaymentMethod method = PaymentMethod.Cash, Guid? customerId = null) =>
        new RegisterCreditPaymentHandler(
                _sessions, _creditAccounts, _payments, _documentNumbers, _unitOfWork,
                new FakeCurrentPharmacy(_pharmacyId), Cashier(userId), new FakeClock(Now),
                new CreditPaymentReader(_customers, _users))
            .Handle(new RegisterCreditPaymentCommand(customerId ?? _customer.Id, method, amount), CancellationToken.None);

    // ---------- Selling on credit ----------

    [Fact]
    public async Task ASaleOnCredit_ChargesTheCustomersAccount_AndTheTicketNamesTheCustomer()
    {
        var ticket = await SellAsync(2,
            [new RegisterSalePayment(PaymentMethod.Cash, 10_000m), new RegisterSalePayment(PaymentMethod.Credit, 14_000m)],
            _customer.Id);

        Assert.Equal(14_000m, _account.Balance);
        Assert.Equal(("Doña Rosa", 14_000m), (ticket.CustomerName, ticket.CreditAmount));
        Assert.Equal((1, 1), (_unitOfWork.SaveCount, _unitOfWork.CommitCount));
    }

    [Fact]
    public async Task ACustomerWithoutCredit_CannotBuyOnCredit_AndCreditWithoutACustomer_IsRejected()
    {
        var walkIn = Customer.Create(_pharmacyId, "Cliente nuevo", DocumentType.CitizenshipCard, "1000000");
        _customers.Add(walkIn);

        await Assert.ThrowsAsync<ValidationException>(() =>
            SellAsync(1, [new RegisterSalePayment(PaymentMethod.Credit, 12_000m)], walkIn.Id));
        await Assert.ThrowsAsync<DomainException>(() =>
            SellAsync(1, [new RegisterSalePayment(PaymentMethod.Credit, 12_000m)], customerId: null));

        Assert.Empty(_sales.Items);
    }

    [Fact]
    public async Task ACreditSaleOverTheLimit_IsRejected_AndNothingIsSaved()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            SellAsync(5, [new RegisterSalePayment(PaymentMethod.Credit, 60_000m)], _customer.Id));

        Assert.Equal(0m, _account.Balance);
        Assert.Equal((0, 0), (_unitOfWork.SaveCount, _unitOfWork.CommitCount));
    }

    // ---------- Payments ("abonos") ----------

    [Fact]
    public async Task APayment_LowersTheDebt_WithItsOwnNumber_AndTheVoucherSaysWhatIsStillOwed()
    {
        await SellAsync(2, [new RegisterSalePayment(PaymentMethod.Credit, 24_000m)], _customer.Id);

        var voucher = await PayAsync(_ana, 10_000m, PaymentMethod.DigitalWallet);

        Assert.Equal(14_000m, _account.Balance);
        Assert.Equal((1, "Doña Rosa", "Ana Cajera"), (voucher.Number, voucher.CustomerName, voucher.ReceivedByName));
        Assert.Equal((10_000m, 14_000m, PaymentMethod.DigitalWallet), (voucher.Amount, voucher.BalanceAfter, voucher.Method));
        Assert.Equal(_sessions.Items.Single().Id, voucher.CashSessionId);

        var again = await new GetCreditPaymentByIdHandler(_payments, new FakeCurrentPharmacy(_pharmacyId), new CreditPaymentReader(_customers, _users))
            .Handle(new GetCreditPaymentByIdQuery(voucher.Id), CancellationToken.None);
        Assert.Equal(voucher.Number, again.Number);
    }

    [Fact]
    public async Task APayment_NeedsYourOwnShift_AndACustomerWithAnAccount()
    {
        await SellAsync(1, [new RegisterSalePayment(PaymentMethod.Credit, 12_000m)], _customer.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => PayAsync(_beto, 5_000m));
        await Assert.ThrowsAsync<NotFoundException>(() => PayAsync(_ana, 5_000m, customerId: Guid.CreateVersion7()));

        Assert.Equal(12_000m, _account.Balance);
        Assert.Empty(_payments.Items);
    }

    [Fact]
    public async Task GetPaymentById_OfAnotherPharmacy_IsNotFound()
    {
        await SellAsync(1, [new RegisterSalePayment(PaymentMethod.Credit, 12_000m)], _customer.Id);
        var voucher = await PayAsync(_ana, 5_000m);

        var handler = new GetCreditPaymentByIdHandler(_payments, new FakeCurrentPharmacy(Guid.CreateVersion7()), new CreditPaymentReader(_customers, _users));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetCreditPaymentByIdQuery(voucher.Id), CancellationToken.None));
    }

    // ---------- Returns of credit sales ----------

    [Fact]
    public async Task ReturningACreditSale_LowersTheDebt_AndTheVoucherSaysSo()
    {
        var ticket = await SellAsync(2, [new RegisterSalePayment(PaymentMethod.Credit, 24_000m)], _customer.Id);

        var voucher = await new RegisterSaleReturnHandler(
                _sessions, _sales, _returns, _batches, _creditAccounts, _documentNumbers, _unitOfWork,
                new FakeCurrentPharmacy(_pharmacyId), Cashier(_ana), new FakeClock(Now.AddDays(1)),
                new SaleReturnReader(_products, _users, _sales))
            .Handle(new RegisterSaleReturnCommand(ticket.Id, "No le sirvió",
                    [new RegisterSaleReturnLine(ticket.Items.Single().Id, 1, ReturnDisposition.Restock)]),
                CancellationToken.None);

        Assert.Equal((12_000m, 12_000m, 0m), (voucher.Refund, voucher.CreditedToAccount, voucher.CashRefund));
        Assert.Equal(12_000m, _account.Balance);
    }
}
