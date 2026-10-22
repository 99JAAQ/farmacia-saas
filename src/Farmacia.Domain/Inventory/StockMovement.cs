using Farmacia.Domain.Common;

namespace Farmacia.Domain.Inventory;

/// <summary>
/// Kardex entry: one change in a batch's quantity, in base units. Created only by <see cref="Batch"/>.
/// </summary>
public class StockMovement : TenantEntity
{
    private StockMovement() { } // EF Core

    internal StockMovement(
        Batch batch, StockMovementType type, int quantityChange,
        DateTimeOffset occurredAt, Guid? referenceId, string? notes)
        : base(batch.PharmacyId)
    {
        BatchId = batch.Id;
        ProductId = batch.ProductId;
        Type = type;
        QuantityChange = quantityChange;
        BalanceAfter = batch.Quantity;
        OccurredAt = occurredAt;
        ReferenceId = referenceId;
        Notes = notes;
    }

    public Guid BatchId { get; private init; }
    public Guid ProductId { get; private init; }
    public StockMovementType Type { get; private init; }

    /// <summary>Positive for entries, negative for exits.</summary>
    public int QuantityChange { get; private init; }

    public int BalanceAfter { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>The purchase or sale that caused the movement, if any.</summary>
    public Guid? ReferenceId { get; private init; }

    public string? Notes { get; private init; }
}
