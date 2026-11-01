using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Purchases.AddPurchaseItem;
using Farmacia.Application.Purchases.ConfirmPurchase;
using Farmacia.Application.Purchases.CreatePurchaseDraft;
using Farmacia.Application.Purchases.RemovePurchaseItem;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Suppliers;
using Farmacia.Infrastructure.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Persistence;

/// <summary>The purchase use cases against a real SQL Server: transactions, concurrency and unique indexes.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class PurchasePersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private Guid _keeperId;
    private Supplier _supplier = null!;
    private Product _product = null!;
    private SaleUnit _box = null!;

    public async Task InitializeAsync()
    {
        // The container is shared by every test: each one needs its own (valid) NIT.
        var nit = Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value;
        _supplier = Supplier.Create(fixture.PharmacyId, "Distribuidora Andina", nit);
        _product = Product.Create(fixture.PharmacyId, $"Acetaminofén {Guid.CreateVersion7():N}", "Tableta", false);
        _box = _product.AddSaleUnit("Caja x30", 30, 12_000m);

        // A real user: the kardex entry points to whoever received the goods (a foreign key to AspNetUsers).
        var keeper = new ApplicationUser { FullName = "Bodeguero", PharmacyId = fixture.PharmacyId };

        await using var db = fixture.CreateContext();
        db.AddRange(_supplier, _product);
        db.Users.Add(keeper);
        await db.SaveChangesAsync();

        _keeperId = keeper.Id;
        _services = InfrastructureServices.Build(fixture, currentUser: new FixedUser(keeper.Id));
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    /// <summary>One DI scope per call, like separate HTTP requests.</summary>
    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<Guid> DraftWithOneLineAsync()
    {
        var purchaseId = await SendAsync(new CreatePurchaseDraftCommand(
            _supplier.Id, $"FE-{Guid.CreateVersion7():N}"[..20], new DateOnly(2026, 10, 1), PaymentTerm.Cash, null));
        await SendAsync(new AddPurchaseItemCommand(
            purchaseId, _product.Id, _box.Id, 10, 2, 9_000m, 3_000m, VatRate.Exempt, "L1", new DateOnly(2027, 12, 31)));
        return purchaseId;
    }

    [Fact]
    public async Task Confirm_SavesThePurchaseTheBatchAndTheKardex_Together()
    {
        var purchaseId = await DraftWithOneLineAsync();

        await SendAsync(new ConfirmPurchaseCommand(purchaseId, 87_000m));

        await using var db = fixture.CreateContext();
        var purchase = await db.Purchases.Include(p => p.Items).SingleAsync(p => p.Id == purchaseId);
        var item = Assert.Single(purchase.Items);
        var batch = await db.Batches.SingleAsync(b => b.Id == item.BatchId);
        var movement = await db.StockMovements.SingleAsync(m => m.BatchId == batch.Id);

        Assert.Equal(PurchaseStatus.Confirmed, purchase.Status);
        Assert.Equal(241.6667m, item.UnitCostPerBaseUnit);
        Assert.Equal(360, batch.Quantity);
        Assert.Equal(241.6667m, batch.UnitCost);
        Assert.Equal(StockMovementType.Purchase, movement.Type);
        Assert.Equal(purchaseId, movement.ReferenceId);
        Assert.Equal(_keeperId, movement.PerformedBy);
    }

    [Fact]
    public async Task ConfirmingTwiceAtTheSameTime_ReceivesTheGoodsOnlyOnce()
    {
        var purchaseId = await DraftWithOneLineAsync();

        // A double click: two requests load the same draft before either one has saved.
        await using var first = _services.CreateAsyncScope();
        await using var second = _services.CreateAsyncScope();
        var firstPurchase = await first.ServiceProvider.GetRequiredService<IPurchaseRepository>()
            .GetByIdAsync(fixture.PharmacyId, purchaseId, CancellationToken.None);
        var secondPurchase = await second.ServiceProvider.GetRequiredService<IPurchaseRepository>()
            .GetByIdAsync(fixture.PharmacyId, purchaseId, CancellationToken.None);

        await ConfirmAndSaveAsync(first.ServiceProvider, firstPurchase!);

        await Assert.ThrowsAsync<ConflictException>(() => ConfirmAndSaveAsync(second.ServiceProvider, secondPurchase!));

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Batches.CountAsync(b => b.ProductId == _product.Id));
        Assert.Equal(1, await db.StockMovements.CountAsync(m => m.ProductId == _product.Id));
    }

    private static async Task ConfirmAndSaveAsync(IServiceProvider services, Purchase purchase)
    {
        var batches = purchase.Confirm(87_000m, vatIsPartOfCost: false, new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.FromHours(-5)));
        foreach (var batch in batches)
            services.GetRequiredService<IBatchRepository>().Add(batch);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    [Fact]
    public async Task TheSameInvoiceTwice_IsStoppedByTheUniqueIndex_EvenIfTheCheckWasSkipped()
    {
        // Two simultaneous requests can both pass the "already registered?" check; the index is the final guard.
        await AddDraftDirectlyAsync("FE-DOBLE");

        await Assert.ThrowsAsync<ConflictException>(() => AddDraftDirectlyAsync("fe-doble"));
    }

    private async Task AddDraftDirectlyAsync(string invoiceNumber)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IPurchaseRepository>().Add(Purchase.CreateDraft(
            fixture.PharmacyId, _supplier.Id, invoiceNumber, new DateOnly(2026, 10, 1), PaymentTerm.Cash));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    [Fact]
    public async Task RemoveItem_DeletesTheLineFromTheDatabase()
    {
        var purchaseId = await DraftWithOneLineAsync();
        Guid itemId;
        await using (var db = fixture.CreateContext())
            itemId = (await db.Purchases.Include(p => p.Items).SingleAsync(p => p.Id == purchaseId)).Items.Single().Id;

        await SendAsync(new RemovePurchaseItemCommand(purchaseId, itemId));

        await using var verify = fixture.CreateContext();
        Assert.False(await verify.Set<PurchaseItem>().AnyAsync(i => i.Id == itemId));
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;

        public bool IsInRole(string role) => role == Roles.StockKeeper;
    }
}
