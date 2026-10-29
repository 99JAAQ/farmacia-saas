using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Cash.CloseCashSession;
using Farmacia.Application.Cash.OpenCashSession;
using Farmacia.Application.SaleReturns.RegisterSaleReturn;
using Farmacia.Application.Sales;
using Farmacia.Application.Sales.RegisterSale;
using Farmacia.Domain.Common;
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
/// Returns against a real SQL Server: return, batch and kardex in one transaction; their own number sequence;
/// the refund in the shift's cash; two simultaneous returns of one ticket. Each test uses its own pharmacy.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SaleReturnPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private Guid _pharmacyId;
    private ServiceProvider _services = null!;
    private Guid _shiftId;

    public async Task InitializeAsync()
    {
        var pharmacy = Pharmacy.Create("Droguería de Devoluciones", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
        var cashier = new ApplicationUser { FullName = "Ana Cajera", PharmacyId = pharmacy.Id };

        await using (var db = fixture.CreateContext())
        {
            db.Pharmacies.Add(pharmacy);
            db.Users.Add(cashier);
            await db.SaveChangesAsync();
        }

        _pharmacyId = pharmacy.Id;
        _services = InfrastructureServices.Build(fixture, pharmacyId: _pharmacyId, currentUser: new FixedUser(cashier.Id));
        _shiftId = await SendAsync(new OpenCashSessionCommand(100_000m));
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>A ticket of the given boxes (30 tablets at 12.000), paid in cash, from a batch of 90.</summary>
    private async Task<(SaleDto Ticket, Batch Batch)> SellBoxesAsync(int boxes)
    {
        var product = Product.Create(_pharmacyId, $"Acetaminofén {Guid.CreateVersion7():N}", "Tableta", false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        var batch = Batch.Receive(_pharmacyId, product.Id, "L1", new DateOnly(2030, 12, 31), 90, 250m,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, SqlServerFixture.Colombia));
        await using (var db = fixture.CreateContext())
        {
            db.AddRange(product, batch);
            await db.SaveChangesAsync();
        }

        var ticket = await SendAsync(new RegisterSaleCommand(
            [new RegisterSaleLine(product.Id, box.Id, boxes)], [new RegisterSalePayment(PaymentMethod.Cash, boxes * 12_000m)], false));
        return (ticket, batch);
    }

    private static RegisterSaleReturnCommand ReturnBoxes(SaleDto ticket, int boxes, ReturnDisposition disposition = ReturnDisposition.Restock) =>
        new(ticket.Id, "El cliente se equivocó", [new RegisterSaleReturnLine(ticket.Items.Single().Id, boxes, disposition)]);

    [Fact]
    public async Task AReturn_IsSavedWholly_WithTheBatchAndTheKardex_AndHasItsOwnNumbers()
    {
        var (ticket, batch) = await SellBoxesAsync(3);

        var voucher = await SendAsync(ReturnBoxes(ticket, 1));

        await using var db = fixture.CreateContext();
        var saleReturn = await db.SaleReturns.Include(r => r.Items).ThenInclude(i => i.Batches).SingleAsync(r => r.Id == voucher.Id);
        Assert.Equal((1, 12_000m), (saleReturn.Number, saleReturn.Refund));
        var restocked = Assert.Single(Assert.Single(saleReturn.Items).Batches);
        Assert.Equal((batch.Id, 30), (restocked.BatchId, restocked.Quantity));

        Assert.Equal(30, (await db.Batches.SingleAsync(b => b.Id == batch.Id)).Quantity); // 90 − 90 + 30
        var movement = await db.StockMovements.SingleAsync(m => m.BatchId == batch.Id && m.Type == StockMovementType.CustomerReturn);
        Assert.Equal((30, 30, voucher.Id), (movement.QuantityChange, movement.BalanceAfter, movement.ReferenceId));

        // Returns and tickets count separately: the next ticket is still N.º 2.
        var (next, _) = await SellBoxesAsync(1);
        Assert.Equal(2, next.Number);
    }

    [Fact]
    public async Task ClosingTheShift_SubtractsTheRefunds()
    {
        var (ticket, _) = await SellBoxesAsync(2); // 24.000 in
        await SendAsync(ReturnBoxes(ticket, 1, ReturnDisposition.Discard)); // 12.000 out

        var closed = await SendAsync(new CloseCashSessionCommand(_shiftId, 112_000m, null));

        Assert.Equal(112_000m, closed.ExpectedCash); // 100.000 float + 24.000 − 12.000
        Assert.Equal(0m, closed.Difference);
    }

    [Fact]
    public async Task TwoSimultaneousReturnsOfTheSameTicket_NeverReturnMoreThanWasSold()
    {
        var (ticket, batch) = await SellBoxesAsync(2);

        // Each one alone is valid (2 of 2 boxes); together they would return 4.
        var results = await Task.WhenAll(
            Attempt(() => SendAsync(ReturnBoxes(ticket, 2))),
            Attempt(() => SendAsync(ReturnBoxes(ticket, 2))));

        Assert.Single(results, succeeded => succeeded);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.SaleReturns.CountAsync(r => r.SaleId == ticket.Id));
        Assert.Equal(90, (await db.Batches.SingleAsync(b => b.Id == batch.Id)).Quantity); // back to 90, not 150
    }

    private static async Task<bool> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (DomainException)
        {
            return false; // "Esa línea del tiquete ya se devolvió completa."
        }
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;

        public bool IsInRole(string role) => role == Roles.Cashier;
    }
}
