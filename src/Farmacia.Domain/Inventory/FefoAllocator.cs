using Farmacia.Domain.Common;

namespace Farmacia.Domain.Inventory;

public sealed record BatchAllocation(Batch Batch, int Quantity);

/// <summary>
/// Domain service: decides which batches a sale takes stock from, First Expired, First Out.
/// It only plans; the caller applies each allocation with <see cref="Batch.Sell"/>.
/// </summary>
public static class FefoAllocator
{
    public static IReadOnlyList<BatchAllocation> Allocate(IEnumerable<Batch> batches, int quantity, DateOnly date)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad debe ser mayor que cero.");

        var all = batches.ToList();

        // A programming error, not a business rule: the caller must load batches for a single product.
        if (all.Select(b => b.ProductId).Distinct().Count() > 1)
            throw new ArgumentException("Todos los lotes deben ser del mismo producto.", nameof(batches));

        var candidates = all
            .Where(b => b.Quantity > 0 && !b.IsExpiredOn(date))
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.ReceivedAt)
            .ToList();

        var available = candidates.Sum(b => b.Quantity);
        if (available < quantity)
            throw new DomainException($"Stock insuficiente: disponible {available}, solicitado {quantity}.");

        var allocations = new List<BatchAllocation>();
        var remaining = quantity;

        foreach (var batch in candidates)
        {
            var taken = Math.Min(batch.Quantity, remaining);
            allocations.Add(new BatchAllocation(batch, taken));
            remaining -= taken;

            if (remaining == 0)
                break;
        }

        return allocations;
    }
}
