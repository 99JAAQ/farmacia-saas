using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Sales;

/// <summary>Builds the ticket DTO with product, sale unit and cashier names (they live in other aggregates).</summary>
public sealed class SaleReader(IProductRepository products, IUserReader users, ISaleReturnRepository returns)
{
    public async Task<SaleDto> ToDtoAsync(Sale sale, CancellationToken cancellationToken)
    {
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var catalog = (await products.GetByIdsAsync(sale.PharmacyId, productIds, cancellationToken)).ToDictionary(p => p.Id);
        var names = await users.GetNamesAsync(sale.PharmacyId, [sale.SoldBy], cancellationToken);
        var returned = (await returns.ListBySaleAsync(sale.PharmacyId, sale.Id, cancellationToken))
            .SelectMany(r => r.Items)
            .GroupBy(i => i.SaleItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        return new SaleDto(
            sale.Id,
            sale.Number,
            sale.SoldAt,
            sale.CashSessionId,
            names.GetValueOrDefault(sale.SoldBy, "Usuario desconocido"),
            sale.Total,
            sale.Vat,
            sale.Change,
            PrescriptionChecked: sale.PrescriptionCheckedBy is not null,
            sale.Items
                .Select(item =>
                {
                    var product = catalog.GetValueOrDefault(item.ProductId);
                    var unit = product?.SaleUnits.SingleOrDefault(u => u.Id == item.SaleUnitId);
                    return new SaleItemDto(
                        item.Id, item.ProductId, product?.Name ?? "Producto desconocido", unit?.Name ?? "",
                        item.Quantity, item.UnitPrice, item.VatRate, item.Total, item.Vat, item.RequiresPrescription,
                        returned.GetValueOrDefault(item.Id));
                })
                .ToList(),
            sale.Payments
                .OrderBy(p => p.Method)
                .Select(p => new PaymentDto(p.Method, p.Amount, p.Received, p.Change))
                .ToList());
    }

    public async Task<IReadOnlyList<SaleSummaryDto>> ToSummariesAsync(
        Guid pharmacyId, IReadOnlyList<Sale> sales, CancellationToken cancellationToken)
    {
        var names = await users.GetNamesAsync(pharmacyId, sales.Select(s => s.SoldBy).Distinct().ToList(), cancellationToken);

        return sales
            .Select(s => new SaleSummaryDto(
                s.Id,
                s.Number,
                s.SoldAt,
                names.GetValueOrDefault(s.SoldBy, "Usuario desconocido"),
                s.Total,
                s.Items.Count,
                s.Payments.Select(p => p.Method).Distinct().Order().ToList()))
            .ToList();
    }
}
