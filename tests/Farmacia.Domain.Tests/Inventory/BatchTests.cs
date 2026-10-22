using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;

namespace Farmacia.Domain.Tests.Inventory;

public class BatchTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid ProductId = Guid.CreateVersion7();
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);
    private static readonly DateOnly ExpirationDate = new(2026, 12, 31);

    private static DateTimeOffset At(int year, int month, int day, int hour = 10) => new(year, month, day, hour, 0, 0, Colombia);

    private static Batch ReceiveBatch(int quantity = 60) =>
        Batch.Receive(PharmacyId, ProductId, "L2026A", ExpirationDate, quantity, 400m, At(2026, 10, 1));

    [Fact]
    public void Receive_CreatesBatch_WithPurchaseMovement()
    {
        var purchaseId = Guid.CreateVersion7();

        var batch = Batch.Receive(PharmacyId, ProductId, " L2026A ", ExpirationDate, 60, 400m, At(2026, 10, 1), purchaseId);

        Assert.Equal("L2026A", batch.LotNumber);
        Assert.Equal(60, batch.Quantity);
        var movement = Assert.Single(batch.PendingMovements);
        Assert.Equal(StockMovementType.Purchase, movement.Type);
        Assert.Equal(60, movement.QuantityChange);
        Assert.Equal(60, movement.BalanceAfter);
        Assert.Equal(batch.Id, movement.BatchId);
        Assert.Equal(ProductId, movement.ProductId);
        Assert.Equal(PharmacyId, movement.PharmacyId);
        Assert.Equal(purchaseId, movement.ReferenceId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Receive_WithNonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<DomainException>(() => ReceiveBatch(quantity));
    }

    [Fact]
    public void Receive_WithNegativeCost_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Batch.Receive(PharmacyId, ProductId, "L1", ExpirationDate, 10, -1m, At(2026, 10, 1)));
    }

    [Fact]
    public void Receive_WithZeroCost_IsAllowed_ForSupplierBonuses()
    {
        var batch = Batch.Receive(PharmacyId, ProductId, "L1", ExpirationDate, 10, 0m, At(2026, 10, 1));

        Assert.Equal(0m, batch.UnitCost);
    }

    [Fact]
    public void Receive_AlreadyExpired_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Batch.Receive(PharmacyId, ProductId, "L1", new DateOnly(2026, 9, 30), 10, 400m, At(2026, 10, 1)));
    }

    [Fact]
    public void Sell_ReducesQuantity_AndRecordsMovement()
    {
        var batch = ReceiveBatch(60);
        var saleId = Guid.CreateVersion7();

        var movement = batch.Sell(10, At(2026, 10, 22), saleId);

        Assert.Equal(50, batch.Quantity);
        Assert.Equal(StockMovementType.Sale, movement.Type);
        Assert.Equal(-10, movement.QuantityChange);
        Assert.Equal(50, movement.BalanceAfter);
        Assert.Equal(saleId, movement.ReferenceId);
        Assert.Equal(2, batch.PendingMovements.Count);
    }

    [Fact]
    public void Sell_MoreThanAvailable_Throws_AndKeepsQuantity()
    {
        var batch = ReceiveBatch(60);

        Assert.Throws<DomainException>(() => batch.Sell(61, At(2026, 10, 22), Guid.CreateVersion7()));
        Assert.Equal(60, batch.Quantity);
        Assert.Single(batch.PendingMovements);
    }

    [Fact]
    public void Sell_OnExpirationDay_IsAllowed()
    {
        var batch = ReceiveBatch();

        batch.Sell(1, At(2026, 12, 31, hour: 20), Guid.CreateVersion7());

        Assert.Equal(59, batch.Quantity);
    }

    [Fact]
    public void Sell_DayAfterExpiration_Throws()
    {
        var batch = ReceiveBatch();

        Assert.Throws<DomainException>(() => batch.Sell(1, At(2027, 1, 1), Guid.CreateVersion7()));
    }

    [Fact]
    public void Sell_UsesLocalDate_NotUtc()
    {
        var batch = ReceiveBatch();
        // 8 p.m. in Colombia on the expiration day is already Jan 1 in UTC: still sellable.
        var eveningInColombia = At(2026, 12, 31, hour: 20);
        Assert.Equal(1, eveningInColombia.UtcDateTime.Day);

        batch.Sell(1, eveningInColombia, Guid.CreateVersion7());

        Assert.Equal(59, batch.Quantity);
    }

    [Fact]
    public void WriteOffExpired_RemovesAllRemainingUnits()
    {
        var batch = ReceiveBatch(60);
        batch.Sell(15, At(2026, 11, 1), Guid.CreateVersion7());

        var movement = batch.WriteOffExpired(At(2027, 1, 2));

        Assert.Equal(0, batch.Quantity);
        Assert.Equal(StockMovementType.ExpiredWriteOff, movement.Type);
        Assert.Equal(-45, movement.QuantityChange);
    }

    [Fact]
    public void WriteOffExpired_WhenNotExpired_Throws()
    {
        var batch = ReceiveBatch();

        Assert.Throws<DomainException>(() => batch.WriteOffExpired(At(2026, 12, 31)));
    }

    [Fact]
    public void AdjustToCount_RecordsTheDifference()
    {
        var batch = ReceiveBatch(60);

        var movement = batch.AdjustToCount(57, "Conteo físico: 3 tabletas dañadas", At(2026, 10, 22));

        Assert.Equal(57, batch.Quantity);
        Assert.Equal(StockMovementType.Adjustment, movement.Type);
        Assert.Equal(-3, movement.QuantityChange);
        Assert.Equal("Conteo físico: 3 tabletas dañadas", movement.Notes);
    }

    [Fact]
    public void AdjustToCount_WithoutReason_Throws()
    {
        var batch = ReceiveBatch(60);

        Assert.Throws<DomainException>(() => batch.AdjustToCount(57, " ", At(2026, 10, 22)));
    }

    [Theory]
    [InlineData(60)] // no difference
    [InlineData(-1)] // negative count
    public void AdjustToCount_WithInvalidCount_Throws(int countedQuantity)
    {
        var batch = ReceiveBatch(60);

        Assert.Throws<DomainException>(() => batch.AdjustToCount(countedQuantity, "Conteo", At(2026, 10, 22)));
    }
}
