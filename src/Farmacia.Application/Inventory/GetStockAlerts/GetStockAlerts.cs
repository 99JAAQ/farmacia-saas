using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Products;
using MediatR;

namespace Farmacia.Application.Inventory.GetStockAlerts;

/// <summary>What needs attention in the inventory today: what to reorder, what to write off and what to sell first.</summary>
public sealed record GetStockAlertsQuery : IRequest<StockAlertsDto>;

/// <param name="LowStock">Below their minimum: to reorder. Fewest units left (relative to the minimum) first.</param>
/// <param name="Expired">Expired with units left: cannot be sold, only written off.</param>
/// <param name="ExpiringSoon">Expire within <see cref="GetStockAlertsHandler.ExpiringWithinDays"/> days: sell them first, or return them to the supplier.</param>
public sealed record StockAlertsDto(
    IReadOnlyList<LowStockAlertDto> LowStock,
    IReadOnlyList<BatchAlertDto> Expired,
    IReadOnlyList<BatchAlertDto> ExpiringSoon)
{
    public int Count => LowStock.Count + Expired.Count + ExpiringSoon.Count;
}

/// <param name="SellableUnits">In base units, in batches that have not expired.</param>
public sealed record LowStockAlertDto(ProductDto Product, int SellableUnits, int MinimumStock);

/// <param name="DaysLeft">Days until it expires; negative once expired.</param>
public sealed record BatchAlertDto(Guid BatchId, ProductDto Product, string LotNumber, DateOnly ExpirationDate, int Quantity, int DaysLeft);

public sealed class GetStockAlertsHandler(
    IProductRepository products,
    IBatchRepository batches,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<GetStockAlertsQuery, StockAlertsDto>
{
    /// <summary>The same horizon the catalog marks as "por vencer".</summary>
    public const int ExpiringWithinDays = 90;

    public async Task<StockAlertsDto> Handle(GetStockAlertsQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var today = DateOnly.FromDateTime(clock.Now.DateTime);

        // Low stock: only products with a minimum (few), and their stock.
        var withMinimum = await products.ListWithMinimumStockAsync(pharmacyId, cancellationToken);
        var stock = (await batches.GetStockAsync(pharmacyId, withMinimum.Select(p => p.Id).ToList(), today, cancellationToken))
            .ToDictionary(s => s.ProductId, s => s.SellableUnits);
        var lowStock = withMinimum
            .Select(p => (Product: p, Units: stock.GetValueOrDefault(p.Id)))
            .Where(x => x.Product.IsBelowMinimum(x.Units))
            .OrderBy(x => (double)x.Units / x.Product.MinimumStock!.Value)
            .ThenBy(x => x.Product.Name)
            .Select(x => new LowStockAlertDto(ProductDto.From(x.Product), x.Units, x.Product.MinimumStock!.Value))
            .ToList();

        // Batches with units that expire before the horizon (expired ones included).
        var expiring = await batches.ListInStockExpiringByAsync(pharmacyId, today.AddDays(ExpiringWithinDays), cancellationToken);
        var batchProducts = (await products.GetByIdsAsync(pharmacyId, expiring.Select(b => b.ProductId).Distinct().ToList(), cancellationToken))
            .ToDictionary(p => p.Id);
        var batchAlerts = expiring
            .Where(b => batchProducts.ContainsKey(b.ProductId))
            .OrderBy(b => b.ExpirationDate)
            .Select(b => new BatchAlertDto(
                b.Id, ProductDto.From(batchProducts[b.ProductId]), b.LotNumber, b.ExpirationDate, b.Quantity,
                b.ExpirationDate.DayNumber - today.DayNumber))
            .ToList();

        return new StockAlertsDto(
            lowStock,
            // A batch is sold through its expiration date (Batch.IsExpiredOn): expired from the day after.
            batchAlerts.Where(b => b.ExpirationDate < today).ToList(),
            batchAlerts.Where(b => b.ExpirationDate >= today).ToList());
    }
}
