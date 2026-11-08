using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Common;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Suppliers;
using Farmacia.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Persistence;

/// <summary>Paying a supplier invoice against a real SQL Server: the balance survives reloading, and under concurrency.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class SupplierPaymentPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset PaidAt = new(2026, 11, 5, 15, 0, 0, SqlServerFixture.Colombia);

    private readonly ServiceProvider _services = InfrastructureServices.Build(fixture);
    private Guid _purchaseId;
    private Guid _ownerId;

    /// <summary>A confirmed credit invoice of 90.000 (10 boxes at 9.000), and a real user (payments point to them).</summary>
    public async Task InitializeAsync()
    {
        var supplier = Supplier.Create(fixture.PharmacyId, "Distribuidora Andina",
            Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
        var product = Product.Create(fixture.PharmacyId, $"Acetaminofén {Guid.CreateVersion7():N}", "Tableta", false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        var owner = new ApplicationUser { FullName = "Dueña", PharmacyId = fixture.PharmacyId };

        var purchase = Purchase.CreateDraft(fixture.PharmacyId, supplier.Id, $"FE-{Guid.CreateVersion7():N}"[..20],
            new DateOnly(2026, 10, 20), PaymentTerm.Credit, new DateOnly(2026, 11, 19));
        purchase.AddItem(product.Id, box.Id, 30, 10, 0, 9_000m, 0m, VatRate.Exempt, "L1", new DateOnly(2028, 1, 31));
        var batches = purchase.Confirm(90_000m, vatIsPartOfCost: false, new DateTimeOffset(2026, 10, 21, 9, 0, 0, SqlServerFixture.Colombia));

        await using var db = fixture.CreateContext();
        db.AddRange(supplier, product, purchase);
        db.AddRange(batches);
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        (_purchaseId, _ownerId) = (purchase.Id, owner.Id);
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    /// <summary>Loads the invoice and pays, in its own scope, like a separate HTTP request.</summary>
    private async Task PayAsync(decimal amount, Func<Task>? beforeSaving = null)
    {
        await using var scope = _services.CreateAsyncScope();
        var purchases = scope.ServiceProvider.GetRequiredService<IPurchaseRepository>();
        var purchase = await purchases.GetByIdAsync(fixture.PharmacyId, _purchaseId, CancellationToken.None);

        purchase!.RegisterPayment(amount, SupplierPaymentMethod.BankTransfer, "TRX-1", PaidAt, _ownerId);
        if (beforeSaving is not null) await beforeSaving();

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ThePaymentsAndTheBalance_SurviveReloadingTheInvoice()
    {
        await PayAsync(60_000m);

        // A second request sees what was paid: 30.000 left, so 30.001 is refused.
        await Assert.ThrowsAsync<DomainException>(() => PayAsync(30_001m));

        await using var db = fixture.CreateContext();
        // The whole aggregate, as its repository loads it: the balance comes from the lines' total.
        var purchase = await db.Purchases.Include(p => p.Items).Include(p => p.Payments).SingleAsync(p => p.Id == _purchaseId);
        Assert.Equal((60_000m, 30_000m), (purchase.AmountPaid, purchase.Balance));
        Assert.Equal(("TRX-1", _ownerId), (Assert.Single(purchase.Payments).Reference, purchase.Payments.Single().PaidBy));
    }

    [Fact]
    public async Task TwoSimultaneousPayments_NeverPayMoreThanTheInvoice()
    {
        // Both load the invoice (90.000 owed) before either saves: each one alone fits, both together would be 120.000.
        var bothLoaded = new TaskCompletionSource();
        var loaded = 0;
        Task WaitForTheOther()
        {
            if (Interlocked.Increment(ref loaded) == 2) bothLoaded.SetResult();
            return bothLoaded.Task;
        }

        var results = await Task.WhenAll(
            Attempt(() => PayAsync(60_000m, WaitForTheOther)),
            Attempt(() => PayAsync(60_000m, WaitForTheOther)));

        Assert.Single(results, succeeded => succeeded);
        await using var db = fixture.CreateContext();
        Assert.Equal(60_000m, (await db.Purchases.SingleAsync(p => p.Id == _purchaseId)).AmountPaid);
        Assert.Equal(1, await db.Set<SupplierPayment>().CountAsync(sp => sp.PurchaseId == _purchaseId));
    }

    private static async Task<bool> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (ConflictException)
        {
            return false; // the purchase's RowVersion changed under it
        }
    }
}
