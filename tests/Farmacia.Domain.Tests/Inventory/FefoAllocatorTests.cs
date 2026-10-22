using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;

namespace Farmacia.Domain.Tests.Inventory;

public class FefoAllocatorTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid ProductId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 10, 22);

    private static Batch NewBatch(string lot, DateOnly expiration, int quantity, int receivedDay = 1, Guid? productId = null) =>
        Batch.Receive(
            PharmacyId, productId ?? ProductId, lot, expiration, quantity, 400m,
            new DateTimeOffset(2026, 10, receivedDay, 10, 0, 0, TimeSpan.FromHours(-5)));

    [Fact]
    public void Allocate_TakesFromEarliestExpirationFirst()
    {
        var late = NewBatch("LATE", new DateOnly(2027, 6, 30), 100);
        var early = NewBatch("EARLY", new DateOnly(2026, 12, 31), 20);

        var allocations = FefoAllocator.Allocate([late, early], 30, Today);

        Assert.Collection(allocations,
            a => { Assert.Same(early, a.Batch); Assert.Equal(20, a.Quantity); },
            a => { Assert.Same(late, a.Batch); Assert.Equal(10, a.Quantity); });
    }

    [Fact]
    public void Allocate_SkipsExpiredAndEmptyBatches()
    {
        var expired = NewBatch("EXPIRED", new DateOnly(2026, 10, 25), 50);
        var empty = NewBatch("EMPTY", new DateOnly(2026, 11, 30), 5);
        empty.Sell(5, new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.FromHours(-5)), Guid.CreateVersion7());
        var valid = NewBatch("VALID", new DateOnly(2027, 1, 31), 50);

        var allocations = FefoAllocator.Allocate([expired, empty, valid], 10, new DateOnly(2026, 10, 26));

        var allocation = Assert.Single(allocations);
        Assert.Same(valid, allocation.Batch);
    }

    [Fact]
    public void Allocate_WithSameExpiration_UsesOldestReceiptFirst()
    {
        var newer = NewBatch("NEWER", new DateOnly(2026, 12, 31), 10, receivedDay: 15);
        var older = NewBatch("OLDER", new DateOnly(2026, 12, 31), 10, receivedDay: 2);

        var allocations = FefoAllocator.Allocate([newer, older], 5, Today);

        Assert.Same(older, Assert.Single(allocations).Batch);
    }

    [Fact]
    public void Allocate_WithInsufficientStock_Throws_AndCountsOnlySellableUnits()
    {
        var expired = NewBatch("EXPIRED", new DateOnly(2026, 10, 25), 50);
        var valid = NewBatch("VALID", new DateOnly(2027, 1, 31), 8);

        var ex = Assert.Throws<DomainException>(() =>
            FefoAllocator.Allocate([expired, valid], 10, new DateOnly(2026, 10, 26)));

        Assert.Contains("disponible 8", ex.Message);
    }

    [Fact]
    public void Allocate_DoesNotModifyBatches()
    {
        var batch = NewBatch("L1", new DateOnly(2026, 12, 31), 20);

        FefoAllocator.Allocate([batch], 5, Today);

        Assert.Equal(20, batch.Quantity);
        Assert.Single(batch.PendingMovements);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Allocate_WithNonPositiveQuantity_Throws(int quantity)
    {
        var batch = NewBatch("L1", new DateOnly(2026, 12, 31), 20);

        Assert.Throws<DomainException>(() => FefoAllocator.Allocate([batch], quantity, Today));
    }

    [Fact]
    public void Allocate_WithBatchesFromDifferentProducts_Throws()
    {
        var a = NewBatch("A", new DateOnly(2026, 12, 31), 20);
        var b = NewBatch("B", new DateOnly(2026, 12, 31), 20, productId: Guid.CreateVersion7());

        Assert.Throws<ArgumentException>(() => FefoAllocator.Allocate([a, b], 5, Today));
    }
}
