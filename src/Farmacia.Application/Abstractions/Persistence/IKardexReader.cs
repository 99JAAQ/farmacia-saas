using Farmacia.Domain.Inventory;

namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>
/// The query side of the kardex: movements with their labels (lot, source document, who), read straight from the
/// database instead of through aggregates. Nothing here changes anything.
/// </summary>
public interface IKardexReader
{
    /// <summary>A product's latest movements, newest first.</summary>
    Task<IReadOnlyList<KardexRow>> ListByProductAsync(Guid pharmacyId, Guid productId, int take, CancellationToken cancellationToken);

    /// <summary>All the product's units now, in every batch: where the running product balance starts.</summary>
    Task<int> GetProductUnitsAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken);
}

/// <param name="BatchBalanceAfter">The batch's units right after the movement (what the kardex stores).</param>
/// <param name="Document">The source document as people say it: "Venta N.º 120", "Compra FE-77"; null for adjustments.</param>
public sealed record KardexRow(
    Guid Id,
    DateTimeOffset OccurredAt,
    StockMovementType Type,
    string LotNumber,
    int QuantityChange,
    int BatchBalanceAfter,
    string? Document,
    string? Notes,
    string? PerformedByName);
