using Farmacia.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class InventoryPersistenceTests(SqlServerFixture fixture)
{
    private static DateTimeOffset At(int month, int day, int hour = 10) =>
        new(2026, month, day, hour, 0, 0, SqlServerFixture.Colombia);

    private async Task<Batch> AddBatchAsync(int quantity = 60)
    {
        var product = await fixture.AddProductAsync();
        var batch = Batch.Receive(
            fixture.PharmacyId, product.Id, "L2026A", new DateOnly(2026, 12, 31), quantity, 333.3333m, At(10, 1));

        await using var db = fixture.CreateContext();
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        return batch;
    }

    [Fact]
    public async Task SaveChanges_PersistsPendingMovements_InTheSameTransaction()
    {
        var batch = await AddBatchAsync(60);

        await using (var db = fixture.CreateContext())
        {
            var loaded = await db.Batches.SingleAsync(b => b.Id == batch.Id);
            loaded.Sell(10, At(10, 22), Guid.CreateVersion7());
            await db.SaveChangesAsync();

            Assert.Empty(loaded.PendingMovements);
        }

        await using var verify = fixture.CreateContext();
        var stored = await verify.Batches.SingleAsync(b => b.Id == batch.Id);
        var movements = await verify.StockMovements
            .Where(m => m.BatchId == batch.Id)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync();

        Assert.Equal(50, stored.Quantity);
        Assert.Equal(333.3333m, stored.UnitCost);
        Assert.Collection(movements,
            m => { Assert.Equal(StockMovementType.Purchase, m.Type); Assert.Equal(60, m.BalanceAfter); },
            m => { Assert.Equal(StockMovementType.Sale, m.Type); Assert.Equal(-10, m.QuantityChange); Assert.Equal(50, m.BalanceAfter); });
    }

    [Fact]
    public async Task Dates_KeepColombianOffset_AndExpirationDate()
    {
        var batch = await AddBatchAsync();

        await using var db = fixture.CreateContext();
        var stored = await db.Batches.SingleAsync(b => b.Id == batch.Id);

        Assert.Equal(new DateOnly(2026, 12, 31), stored.ExpirationDate);
        Assert.Equal(SqlServerFixture.Colombia, stored.ReceivedAt.Offset);
        Assert.Equal(At(10, 1), stored.ReceivedAt);
    }

    [Fact]
    public async Task ConcurrentSales_OnTheSameBatch_SecondOneFails_AndNothingIsLost()
    {
        var batch = await AddBatchAsync(60);

        // Two cashiers load the same batch at the same time.
        await using var cashier1 = fixture.CreateContext();
        await using var cashier2 = fixture.CreateContext();
        var batchForCashier1 = await cashier1.Batches.SingleAsync(b => b.Id == batch.Id);
        var batchForCashier2 = await cashier2.Batches.SingleAsync(b => b.Id == batch.Id);

        batchForCashier1.Sell(10, At(10, 22), Guid.CreateVersion7());
        await cashier1.SaveChangesAsync();

        // Cashier 2 still thinks there are 60. Without RowVersion it would write 50 and lose cashier 1's sale.
        batchForCashier2.Sell(10, At(10, 22), Guid.CreateVersion7());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => cashier2.SaveChangesAsync());

        await using var verify = fixture.CreateContext();
        Assert.Equal(50, (await verify.Batches.SingleAsync(b => b.Id == batch.Id)).Quantity);
        // The failed sale's movement was rolled back with it: purchase + one sale only.
        Assert.Equal(2, await verify.StockMovements.CountAsync(m => m.BatchId == batch.Id));
    }
}
