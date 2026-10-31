using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.SaleReturns;

/// <summary>Builds the voucher DTO with the ticket number and the product, sale unit and cashier names.</summary>
public sealed class SaleReturnReader(IProductRepository products, IUserReader users, ISaleRepository sales)
{
    /// <param name="sale">The returned ticket, when the caller already has it (saves a query).</param>
    public async Task<SaleReturnDto> ToDtoAsync(SaleReturn saleReturn, Sale? sale, CancellationToken cancellationToken)
    {
        sale ??= await sales.GetByIdAsync(saleReturn.PharmacyId, saleReturn.SaleId, cancellationToken);
        var productIds = saleReturn.Items.Select(i => i.ProductId).Distinct().ToList();
        var catalog = (await products.GetByIdsAsync(saleReturn.PharmacyId, productIds, cancellationToken)).ToDictionary(p => p.Id);
        var names = await users.GetNamesAsync(saleReturn.PharmacyId, [saleReturn.ReturnedBy], cancellationToken);

        return new SaleReturnDto(
            saleReturn.Id,
            saleReturn.Number,
            saleReturn.SaleId,
            sale?.Number ?? 0,
            saleReturn.ReturnedAt,
            saleReturn.CashSessionId,
            names.GetValueOrDefault(saleReturn.ReturnedBy, "Usuario desconocido"),
            saleReturn.Reason,
            saleReturn.Refund,
            saleReturn.CreditedToAccount,
            saleReturn.CashRefund,
            saleReturn.Items
                .Select(item =>
                {
                    var product = catalog.GetValueOrDefault(item.ProductId);
                    var unit = product?.SaleUnits.SingleOrDefault(u => u.Id == item.SaleUnitId);
                    return new SaleReturnItemDto(
                        item.Id, item.SaleItemId, product?.Name ?? "Producto desconocido", unit?.Name ?? "",
                        item.Quantity, item.UnitPrice, item.Refund, item.Disposition);
                })
                .ToList());
    }
}
