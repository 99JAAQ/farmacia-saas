using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Inventory.AdjustBatchToCount;
using Farmacia.Application.Inventory.GetProductKardex;
using Farmacia.Application.Inventory.WriteOffExpiredBatch;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Tests.Inventory;

public class InventoryHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset Received = new(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly Guid _keeper = Guid.CreateVersion7();
    private readonly InMemoryBatchRepository _batches = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Product _product;

    public InventoryHandlersTests()
    {
        _product = Product.Create(_pharmacyId, "Loratadina 10 mg", "Tableta", false);
        _products.Add(_product);
    }

    private Batch AddBatch(string lot, DateOnly expiration, int quantity)
    {
        var batch = Batch.Receive(_pharmacyId, _product.Id, lot, expiration, quantity, 250m, Received);
        _batches.Add(batch);
        return batch;
    }

    private Task WriteOffAsync(Guid batchId, Guid? pharmacyId = null) =>
        new WriteOffExpiredBatchHandler(_batches, _unitOfWork, new FakeCurrentPharmacy(pharmacyId ?? _pharmacyId), new FakeCurrentUser(_keeper), new FakeClock(Now))
            .Handle(new WriteOffExpiredBatchCommand(batchId), CancellationToken.None);

    private Task AdjustAsync(Guid batchId, int counted, string reason) =>
        new AdjustBatchToCountHandler(_batches, _unitOfWork, new FakeCurrentPharmacy(_pharmacyId), new FakeCurrentUser(_keeper), new FakeClock(Now))
            .Handle(new AdjustBatchToCountCommand(batchId, counted, reason), CancellationToken.None);

    [Fact]
    public async Task WriteOff_EmptiesAnExpiredBatch_WithWhoDidIt_ButNotOneStillSellable()
    {
        var expired = AddBatch("VIEJO", new DateOnly(2026, 10, 30), 20);
        var today = AddBatch("HOY", new DateOnly(2026, 10, 31), 10); // sellable through today

        await WriteOffAsync(expired.Id);
        Assert.Equal(0, expired.Quantity);
        Assert.Equal((StockMovementType.ExpiredWriteOff, -20, _keeper), (expired.PendingMovements.Last().Type, expired.PendingMovements.Last().QuantityChange, expired.PendingMovements.Last().PerformedBy));

        await Assert.ThrowsAsync<DomainException>(() => WriteOffAsync(today.Id));
        Assert.Equal(10, today.Quantity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Adjust_SetsTheBatchToTheCount_WithTheReason_AndRefusesNoReason()
    {
        var batch = AddBatch("L1", new DateOnly(2028, 1, 31), 50);

        await AdjustAsync(batch.Id, 47, "Tres tabletas dañadas");

        Assert.Equal(47, batch.Quantity);
        var movement = batch.PendingMovements.Last();
        Assert.Equal((StockMovementType.Adjustment, -3, "Tres tabletas dañadas", _keeper), (movement.Type, movement.QuantityChange, movement.Notes, movement.PerformedBy));
        await Assert.ThrowsAsync<DomainException>(() => AdjustAsync(batch.Id, 45, " "));
    }

    [Fact]
    public async Task ABatchOfAnotherPharmacy_IsNotFound()
    {
        var batch = AddBatch("VIEJO", new DateOnly(2026, 10, 30), 20);

        await Assert.ThrowsAsync<NotFoundException>(() => WriteOffAsync(batch.Id, Guid.CreateVersion7()));
        Assert.Equal(20, batch.Quantity);
    }

    [Fact]
    public async Task Kardex_ComputesTheProductBalance_BackwardsFromTodaysStock()
    {
        // Newest first: +30 (lot B), −10 (lot A), +40 (lot A). Today the product has 60.
        var reader = new FakeKardexReader(units: 60,
            Row("B", +30, 30, Now),
            Row("A", -10, 30, Now.AddDays(-1)),
            Row("A", +40, 40, Now.AddDays(-2)));

        var entries = await new GetProductKardexHandler(_products, reader, new FakeCurrentPharmacy(_pharmacyId))
            .Handle(new GetProductKardexQuery(_product.Id), CancellationToken.None);

        Assert.Equal([60, 30, 40], entries.Select(e => e.ProductBalanceAfter));
        Assert.Equal([30, 30, 40], entries.Select(e => e.BatchBalanceAfter));
    }

    private static KardexRow Row(string lot, int change, int batchBalance, DateTimeOffset at) =>
        new(Guid.CreateVersion7(), at, change > 0 ? StockMovementType.Purchase : StockMovementType.Sale, lot, change, batchBalance, null, null, null);

    private sealed class FakeKardexReader(int units, params KardexRow[] rows) : IKardexReader
    {
        public Task<IReadOnlyList<KardexRow>> ListByProductAsync(Guid pharmacyId, Guid productId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KardexRow>>(rows);

        public Task<int> GetProductUnitsAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken) => Task.FromResult(units);
    }
}
