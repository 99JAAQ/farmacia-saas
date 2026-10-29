using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.SaleReturns;
using Farmacia.Application.SaleReturns.GetSaleReturnById;
using Farmacia.Application.SaleReturns.RegisterSaleReturn;
using Farmacia.Application.Sales;
using Farmacia.Application.Sales.GetSaleByNumber;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Cash;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Tests.SaleReturns;

public class SaleReturnHandlersTests
{
    private static readonly DateTimeOffset SoldAt = new(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset Now = SoldAt.AddDays(2);

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
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Batch _batch;
    private readonly Sale _sale;
    private readonly CashSession _shift;

    public SaleReturnHandlersTests()
    {
        _users.Users[_ana] = (_pharmacyId, "Ana Cajera");
        var product = Product.Create(_pharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        _products.Add(product);
        _batch = Batch.Receive(_pharmacyId, product.Id, "L1", new DateOnly(2027, 6, 30), 100, 250m, SoldAt.AddDays(-10));
        _batches.Add(_batch);

        // Sold days ago, in an earlier shift (already closed): the refund comes out of today's shift.
        _sale = Sale.Register(_pharmacyId, 7, Guid.CreateVersion7(), _ana, SoldAt,
            [new SaleLine(product, box.Id, 3, [_batch])], [new PaymentLine(PaymentMethod.Cash, 36_000m)], false);
        _sales.Add(_sale);
        _shift = CashSession.Open(_pharmacyId, _ana, 100_000m, Now.AddHours(-2));
        _sessions.Add(_shift);
    }

    private SaleReturnReader Reader => new(_products, _users, _sales);

    private Task<SaleReturnDto> ReturnAsync(Guid userId, int boxes = 1, ReturnDisposition disposition = ReturnDisposition.Restock, Guid? saleId = null) =>
        new RegisterSaleReturnHandler(
                _sessions, _sales, _returns, _batches, _documentNumbers, _unitOfWork,
                new FakeCurrentPharmacy(_pharmacyId), new FakeCurrentUser(userId, Roles.Cashier), new FakeClock(Now), Reader)
            .Handle(new RegisterSaleReturnCommand(
                    saleId ?? _sale.Id, "Le formularon otro medicamento",
                    [new RegisterSaleReturnLine(_sale.Items.Single().Id, boxes, disposition)]),
                CancellationToken.None);

    [Fact]
    public async Task Register_RefundsFromTheCashiersOpenShift_WithItsOwnNumber_AndCommitsOnce()
    {
        var voucher = await ReturnAsync(_ana);

        var saleReturn = Assert.Single(_returns.Items);
        Assert.Equal(_shift.Id, saleReturn.CashSessionId); // today's shift, not the sale's
        Assert.Equal(Now, saleReturn.ReturnedAt);
        Assert.Equal(40, _batch.Quantity); // 100 − 90 + 30
        Assert.Equal((1, 1), (_unitOfWork.SaveCount, _unitOfWork.CommitCount));

        Assert.Equal((1, 7), (voucher.Number, voucher.SaleNumber)); // its own sequence, not the tickets'
        Assert.Equal(("Ana Cajera", 12_000m), (voucher.ReturnedByName, voucher.Refund));
        var item = Assert.Single(voucher.Items);
        Assert.Equal(("Acetaminofén 500 mg", "Caja x30", ReturnDisposition.Restock), (item.ProductName, item.SaleUnitName, item.Disposition));
    }

    [Fact]
    public async Task Register_CountsTheEarlierReturns_AndTheTicketShowsWhatWasReturned()
    {
        await ReturnAsync(_ana, boxes: 2, disposition: ReturnDisposition.Discard);

        await Assert.ThrowsAsync<Domain.Common.DomainException>(() => ReturnAsync(_ana, boxes: 2));

        var ticket = await new GetSaleByNumberHandler(_sales, new FakeCurrentPharmacy(_pharmacyId), new SaleReader(_products, _users, _returns))
            .Handle(new GetSaleByNumberQuery(7), CancellationToken.None);
        Assert.Equal((3, 2), (ticket.Items.Single().Quantity, ticket.Items.Single().ReturnedQuantity));
    }

    [Fact]
    public async Task Register_InSomeoneElsesShift_IsForbidden_AndWithoutAShift_IsAConflict()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => ReturnAsync(_beto));

        _shift.Close(100_000m, 0m, _ana, Now);
        await Assert.ThrowsAsync<ConflictException>(() => ReturnAsync(_ana));

        Assert.Empty(_returns.Items);
        Assert.Equal(10, _batch.Quantity);
    }

    [Fact]
    public async Task Register_AnUnknownSale_IsNotFound_AndAnEmptyReturn_IsInvalid()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ReturnAsync(_ana, saleId: Guid.CreateVersion7()));

        var handler = new RegisterSaleReturnHandler(
            _sessions, _sales, _returns, _batches, _documentNumbers, _unitOfWork,
            new FakeCurrentPharmacy(_pharmacyId), new FakeCurrentUser(_ana, Roles.Cashier), new FakeClock(Now), Reader);
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new RegisterSaleReturnCommand(_sale.Id, "Motivo", []), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_ReturnsTheVoucher_OnlyInItsPharmacy()
    {
        var voucher = await ReturnAsync(_ana);
        GetSaleReturnByIdHandler Handler(Guid pharmacyId) => new(_returns, new FakeCurrentPharmacy(pharmacyId), Reader);

        var found = await Handler(_pharmacyId).Handle(new GetSaleReturnByIdQuery(voucher.Id), CancellationToken.None);
        Assert.Equal((voucher.Number, 7), (found.Number, found.SaleNumber));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(Guid.CreateVersion7()).Handle(new GetSaleReturnByIdQuery(voucher.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetSaleByNumber_OfAnotherPharmacy_IsNotFound()
    {
        var handler = new GetSaleByNumberHandler(_sales, new FakeCurrentPharmacy(Guid.CreateVersion7()), new SaleReader(_products, _users, _returns));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetSaleByNumberQuery(7), CancellationToken.None));
    }
}
