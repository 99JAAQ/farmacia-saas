using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Sales;
using Farmacia.Application.Sales.GetSaleById;
using Farmacia.Application.Sales.ListSales;
using Farmacia.Application.Sales.RegisterSale;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Cash;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Tests.Sales;

public class SaleHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly Guid _ana = Guid.CreateVersion7();
    private readonly Guid _beto = Guid.CreateVersion7();

    private readonly InMemoryCashSessionRepository _sessions = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryBatchRepository _batches = new();
    private readonly InMemorySaleRepository _sales = new();
    private readonly InMemorySaleReturnRepository _returns = new();
    private readonly FakeDocumentNumberSequence _documentNumbers = new();
    private readonly FakeUserReader _users = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryCreditAccountRepository _creditAccounts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Product _product;
    private readonly SaleUnit _box;
    private readonly Batch _batch;

    public SaleHandlersTests()
    {
        _users.Users[_ana] = (_pharmacyId, "Ana Cajera");
        _product = Product.Create(_pharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
        _box = _product.AddSaleUnit("Caja x30", 30, 12_000m);
        _products.Add(_product);
        _batch = Batch.Receive(_pharmacyId, _product.Id, "L1", new DateOnly(2027, 6, 30), 90, 241.6667m, Now.AddDays(-10));
        _batches.Add(_batch);
        _sessions.Add(CashSession.Open(_pharmacyId, _ana, 100_000m, Now.AddHours(-3)));
    }

    private readonly FakeRealtimeNotifications _realtime = new();

    private Task<SaleDto> SellAsync(Guid userId, int boxes = 1, decimal cash = 50_000m, Guid? productId = null) =>
        new RegisterSaleHandler(
                _sessions, _products, _batches, _sales, _creditAccounts, _documentNumbers, _unitOfWork,
                new FakeCurrentPharmacy(_pharmacyId), new FakeCurrentUser(userId, Roles.Cashier), new FakeClock(Now),
                new SaleReader(_products, _users, _returns, _customers), _realtime)
            .Handle(new RegisterSaleCommand(
                    [new RegisterSaleLine(productId ?? _product.Id, _box.Id, boxes)],
                    [new RegisterSalePayment(PaymentMethod.Cash, cash)],
                    PrescriptionChecked: false),
                CancellationToken.None);

    [Fact]
    public async Task Register_SellsInTheCashiersShift_WithTheNextTicketNumber_AndCommitsOnce()
    {
        var ticket = await SellAsync(_ana);

        var sale = Assert.Single(_sales.Items);
        Assert.Equal(1, ticket.Number);
        Assert.Equal(_sessions.Items.Single().Id, sale.CashSessionId);
        Assert.Equal(Now, sale.SoldAt);
        Assert.Equal(60, _batch.Quantity);
        Assert.Equal((1, 1), (_unitOfWork.SaveCount, _unitOfWork.CommitCount));
        // Open screens hear about it: the day's sales and the product's stock (sent after the commit).
        Assert.Equal([("sales", (Guid?)sale.Id), ("stock", _product.Id)], _realtime.Added);

        // The ticket: names and change, no costs.
        Assert.Equal("Ana Cajera", ticket.SoldByName);
        var item = Assert.Single(ticket.Items);
        Assert.Equal(("Acetaminofén 500 mg", "Caja x30"), (item.ProductName, item.SaleUnitName));
        Assert.Equal(38_000m, ticket.Change);
    }

    [Fact]
    public async Task Register_NumbersTheTicketsConsecutively()
    {
        var first = await SellAsync(_ana);
        var second = await SellAsync(_ana);

        Assert.Equal((1, 2), (first.Number, second.Number));
    }

    [Fact]
    public async Task Register_InSomeoneElsesShift_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => SellAsync(_beto));
        Assert.Empty(_sales.Items);
        Assert.Equal(90, _batch.Quantity);
    }

    [Fact]
    public async Task Register_WithTheRegisterClosed_IsAConflict()
    {
        _sessions.Items.Single().Close(100_000m, 0m, _ana, Now);

        await Assert.ThrowsAsync<ConflictException>(() => SellAsync(_ana));
    }

    [Fact]
    public async Task Register_AProductOfAnotherPharmacy_IsInvalid()
    {
        var foreign = Product.Create(Guid.CreateVersion7(), "Otro", "Tableta", false);
        _products.Add(foreign);

        await Assert.ThrowsAsync<ValidationException>(() => SellAsync(_ana, productId: foreign.Id));
    }

    [Fact]
    public async Task Register_WhenTheDomainRejectsTheSale_NothingIsSavedNorCommitted()
    {
        // 4 boxes = 120 tablets, only 90 in stock.
        await Assert.ThrowsAsync<DomainException>(() => SellAsync(_ana, boxes: 4, cash: 50_000m));

        Assert.Empty(_sales.Items);
        Assert.Equal((0, 0), (_unitOfWork.SaveCount, _unitOfWork.CommitCount));
    }

    [Fact]
    public async Task GetById_ReturnsTheTicket_OnlyInItsPharmacy()
    {
        var ticket = await SellAsync(_ana);
        GetSaleByIdHandler Handler(Guid pharmacyId) =>
            new(_sales, new FakeCurrentPharmacy(pharmacyId), new SaleReader(_products, _users, _returns, _customers));

        var found = await Handler(_pharmacyId).Handle(new GetSaleByIdQuery(ticket.Id), CancellationToken.None);
        Assert.Equal(ticket.Number, found.Number);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(Guid.CreateVersion7()).Handle(new GetSaleByIdQuery(ticket.Id), CancellationToken.None));
    }

    [Fact]
    public async Task List_ReturnsTheShiftsSales_NewestFirst_WithTheirPaymentMethods()
    {
        var first = await SellAsync(_ana);
        var second = await SellAsync(_ana);
        var shiftId = _sessions.Items.Single().Id;

        var list = await new ListSalesHandler(_sales, new FakeCurrentPharmacy(_pharmacyId), new SaleReader(_products, _users, _returns, _customers))
            .Handle(new ListSalesQuery(shiftId), CancellationToken.None);

        Assert.Equal([second.Number, first.Number], list.Select(s => s.Number));
        Assert.Equal(("Ana Cajera", 12_000m, 1), (list[0].SoldByName, list[0].Total, list[0].ItemCount));
        Assert.Equal([PaymentMethod.Cash], list[0].Methods);
    }
}
