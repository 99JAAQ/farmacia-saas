using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Cash.CloseCashSession;
using Farmacia.Application.Cash.OpenCashSession;
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
/// Sales against a real SQL Server: one transaction for sale, stock, kardex and ticket number; the counter under
/// concurrency; a failed sale does not use up a number; the shift's cash comes from the sales.
/// Each test uses its own pharmacy (its own register and its own ticket numbers).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SalePersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private Guid _pharmacyId;
    private ServiceProvider _services = null!;
    private Guid _shiftId;

    public async Task InitializeAsync()
    {
        var pharmacy = Pharmacy.Create("Droguería de Ventas", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
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

    /// <summary>A product sold by boxes of 30 at 12.000, with one batch of the given tablets.</summary>
    private async Task<(Product Product, SaleUnit Box, Batch Batch)> SeedProductAsync(int tablets = 90)
    {
        var product = Product.Create(_pharmacyId, $"Acetaminofén {Guid.CreateVersion7():N}", "Tableta", false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        var batch = Batch.Receive(_pharmacyId, product.Id, "L1", new DateOnly(2030, 12, 31), tablets, 241.6667m,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, SqlServerFixture.Colombia));

        await using var db = fixture.CreateContext();
        db.AddRange(product, batch);
        await db.SaveChangesAsync();
        return (product, box, batch);
    }

    private static RegisterSaleCommand SellBoxes(Product product, SaleUnit box, int boxes, decimal cash) =>
        new([new RegisterSaleLine(product.Id, box.Id, boxes)], [new RegisterSalePayment(PaymentMethod.Cash, cash)], false);

    [Fact]
    public async Task ASale_IsSavedWholly_WithTheStockAndTheKardex_InOneTransaction()
    {
        var (product, box, batch) = await SeedProductAsync(tablets: 90);

        var ticket = await SendAsync(SellBoxes(product, box, 2, 50_000m));

        await using var db = fixture.CreateContext();
        var sale = await db.Sales.Include(s => s.Items).ThenInclude(i => i.Batches).Include(s => s.Payments)
            .SingleAsync(s => s.Id == ticket.Id);
        Assert.Equal(1, sale.Number);
        Assert.Equal(24_000m, sale.Total);
        var item = Assert.Single(sale.Items);
        Assert.Equal((batch.Id, 60), (Assert.Single(item.Batches).BatchId, item.Batches.Single().Quantity));
        Assert.Equal(14_500.00m, item.Cost); // 60 × 241,6667, rounded to cents
        var payment = Assert.Single(sale.Payments);
        Assert.Equal((24_000m, 50_000m), (payment.Amount, payment.Received));

        Assert.Equal(30, (await db.Batches.SingleAsync(b => b.Id == batch.Id)).Quantity);
        var movement = await db.StockMovements.SingleAsync(m => m.BatchId == batch.Id && m.Type == StockMovementType.Sale);
        Assert.Equal((-60, 30, ticket.Id), (movement.QuantityChange, movement.BalanceAfter, movement.ReferenceId));
    }

    [Fact]
    public async Task AFailedSale_DoesNotUseUpATicketNumber()
    {
        var (product, box, _) = await SeedProductAsync(tablets: 30);

        // 2 boxes with stock for 1: the domain rejects it after the number was reserved, and the rollback gives it back.
        await Assert.ThrowsAsync<DomainException>(() => SendAsync(SellBoxes(product, box, 2, 50_000m)));
        var ticket = await SendAsync(SellBoxes(product, box, 1, 12_000m));

        Assert.Equal(1, ticket.Number);
    }

    [Fact]
    public async Task SimultaneousSales_GetConsecutiveNumbers_WithoutRepeatingNorFailing()
    {
        // Different products: each sale takes its own batch, so only the ticket counter is shared.
        var products = new List<(Product Product, SaleUnit Box, Batch Batch)>();
        for (var i = 0; i < 5; i++)
            products.Add(await SeedProductAsync());

        var tickets = await Task.WhenAll(products.Select(p => SendAsync(SellBoxes(p.Product, p.Box, 1, 12_000m))));

        Assert.Equal([1L, 2, 3, 4, 5], tickets.Select(t => t.Number).Order().ToArray());
    }

    [Fact]
    public async Task ClosingTheShift_CountsTheCashOfItsSales()
    {
        var (product, box, _) = await SeedProductAsync();
        await SendAsync(SellBoxes(product, box, 1, 50_000m)); // 12.000 stays in the drawer, 38.000 goes back as change

        var closed = await SendAsync(new CloseCashSessionCommand(_shiftId, 112_000m, null));

        Assert.Equal(112_000m, closed.ExpectedCash); // 100.000 float + 12.000 (not the 50.000 handed over)
        Assert.Equal(0m, closed.Difference);
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;

        public bool IsInRole(string role) => role == Roles.Cashier;
    }
}
