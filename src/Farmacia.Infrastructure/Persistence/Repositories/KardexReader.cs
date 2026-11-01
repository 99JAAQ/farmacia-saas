using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// Labels each movement with a few small lookups (lots, documents, users) instead of one big join: the movements are
/// few (one page) and each lookup is by primary key.
/// </summary>
internal sealed class KardexReader(FarmaciaDbContext db) : IKardexReader
{
    public async Task<IReadOnlyList<KardexRow>> ListByProductAsync(
        Guid pharmacyId, Guid productId, int take, CancellationToken cancellationToken)
    {
        var movements = await db.StockMovements
            .AsNoTracking()
            .Where(m => m.PharmacyId == pharmacyId && m.ProductId == productId)
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id) // Guid v7: two movements at the same instant keep their order
            .Take(take)
            .ToListAsync(cancellationToken);

        var batchIds = movements.Select(m => m.BatchId).Distinct().ToList();
        var lots = await db.Batches
            .Where(b => b.PharmacyId == pharmacyId && batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.LotNumber, cancellationToken);

        var references = movements.Select(m => m.ReferenceId).OfType<Guid>().Distinct().ToList();
        var purchases = await db.Purchases
            .Where(p => p.PharmacyId == pharmacyId && references.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.InvoiceNumber, cancellationToken);
        var sales = await db.Sales
            .Where(s => s.PharmacyId == pharmacyId && references.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Number, cancellationToken);
        var returns = await db.SaleReturns
            .Where(r => r.PharmacyId == pharmacyId && references.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Number, cancellationToken);

        var userIds = movements.Select(m => m.PerformedBy).OfType<Guid>().Distinct().ToList();
        var users = await db.Users
            .Where(u => u.PharmacyId == pharmacyId && userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        string? DocumentOf(StockMovement m) => m.ReferenceId is not { } id ? null : m.Type switch
        {
            StockMovementType.Purchase when purchases.TryGetValue(id, out var invoice) => $"Compra {invoice}",
            StockMovementType.Sale when sales.TryGetValue(id, out var number) => $"Venta N.º {number}",
            StockMovementType.CustomerReturn when returns.TryGetValue(id, out var number) => $"Devolución N.º {number}",
            _ => null,
        };

        return movements
            .Select(m => new KardexRow(
                m.Id, m.OccurredAt, m.Type, lots.GetValueOrDefault(m.BatchId, ""), m.QuantityChange, m.BalanceAfter,
                DocumentOf(m), m.Notes, m.PerformedBy is { } user ? users.GetValueOrDefault(user) : null))
            .ToList();
    }

    public async Task<int> GetProductUnitsAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        await db.Batches
            .Where(b => b.PharmacyId == pharmacyId && b.ProductId == productId)
            .SumAsync(b => b.Quantity, cancellationToken);
}
