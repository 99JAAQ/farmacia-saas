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
        DateTimeOffset occurredAt, Guid? referenceId, string? notes, Guid? performedBy)
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
        PerformedBy = performedBy;
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

    /// <summary>
    /// The user who caused it: who received, sold, refunded, wrote off or adjusted. For a write-off or an adjustment
    /// it is the first thing the owner asks. Null in movements recorded before it existed.
    /// </summary>
    public Guid? PerformedBy { get; private init; }
}
