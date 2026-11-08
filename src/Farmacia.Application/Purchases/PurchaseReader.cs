using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Purchases;

/// <summary>Builds the purchase DTOs, adding the product and supplier names the screens need.</summary>
public sealed class PurchaseReader(IProductRepository products, ISupplierRepository suppliers, IUserReader users, IClock clock)
{
    // Shown if a referenced record cannot be found; it cannot happen with the FKs in place, but never crash a read.
    private const string Unknown = "(desconocido)";

    public async Task<PurchaseDto> ToDtoAsync(Purchase purchase, CancellationToken cancellationToken)
    {
        var productIds = purchase.Items.Select(i => i.ProductId).Distinct().ToList();
        var productsById = (await products.GetByIdsAsync(purchase.PharmacyId, productIds, cancellationToken))
            .ToDictionary(p => p.Id);
        var supplierName = await SupplierNamesAsync(purchase.PharmacyId, [purchase.SupplierId], cancellationToken);

        var items = purchase.Items
            .Select(item =>
            {
                var product = productsById.GetValueOrDefault(item.ProductId);
                var saleUnit = product?.SaleUnits.FirstOrDefault(u => u.Id == item.SaleUnitId);
                return new PurchaseItemDto(
                    item.Id, item.ProductId, product?.Name ?? Unknown, item.SaleUnitId, saleUnit?.Name ?? Unknown,
                    item.UnitsPerPackage, item.Quantity, item.BonusQuantity, item.UnitCost, item.LineDiscount,
                    item.VatRate, item.LotNumber, item.ExpirationDate, item.BaseUnits, item.NetAmount,
                    item.BatchId, item.UnitCostPerBaseUnit);
            })
            .ToList();

        var payers = await users.GetNamesAsync(
            purchase.PharmacyId, purchase.Payments.Select(p => p.PaidBy).Distinct().ToList(), cancellationToken);
        var payments = purchase.Payments
            .OrderBy(p => p.PaidAt)
            .Select(p => new SupplierPaymentDto(p.Id, p.Amount, p.Method, p.Reference, p.PaidAt, payers.GetValueOrDefault(p.PaidBy, Unknown)))
            .ToList();

        return new PurchaseDto(
            purchase.Id, purchase.SupplierId, supplierName.GetValueOrDefault(purchase.SupplierId, Unknown),
            purchase.InvoiceNumber, purchase.InvoiceDate, purchase.PaymentTerm, purchase.DueDate,
            purchase.InvoiceDiscount, purchase.Status, purchase.ConfirmedAt, purchase.CalculateTotals(), items,
            purchase.AmountPaid, purchase.Balance, purchase.IsOverdueOn(Today), payments);
    }

    public async Task<IReadOnlyList<PurchaseSummaryDto>> ToSummariesAsync(
        Guid pharmacyId, IReadOnlyList<Purchase> purchases, CancellationToken cancellationToken)
    {
        var supplierNames = await SupplierNamesAsync(
            pharmacyId, purchases.Select(p => p.SupplierId).Distinct().ToList(), cancellationToken);

        return purchases
            .Select(p => new PurchaseSummaryDto(
                p.Id, supplierNames.GetValueOrDefault(p.SupplierId, Unknown), p.InvoiceNumber, p.InvoiceDate,
                p.Status, p.Items.Count, p.CalculateTotals().Total))
            .ToList();
    }

    /// <summary>What is owed, the most overdue first, then by due date: the order to pay in.</summary>
    public async Task<IReadOnlyList<PayableInvoiceDto>> ToPayablesAsync(
        Guid pharmacyId, IReadOnlyList<Purchase> purchases, CancellationToken cancellationToken)
    {
        var owed = purchases.Where(p => p.Balance > 0).ToList();
        var supplierNames = await SupplierNamesAsync(pharmacyId, owed.Select(p => p.SupplierId).Distinct().ToList(), cancellationToken);
        var today = Today;

        return owed
            .Select(p =>
            {
                int? daysToDue = p.DueDate is { } due ? due.DayNumber - today.DayNumber : null;
                return new PayableInvoiceDto(
                    p.Id, p.SupplierId, supplierNames.GetValueOrDefault(p.SupplierId, Unknown), p.InvoiceNumber, p.InvoiceDate,
                    p.DueDate, p.CalculateTotals().Total, p.AmountPaid, p.Balance, p.IsOverdueOn(today),
                    DaysOverdue: Math.Max(-(daysToDue ?? 0), 0), daysToDue);
            })
            .OrderByDescending(p => p.DaysOverdue)
            .ThenBy(p => p.DueDate ?? DateOnly.MaxValue)
            .ToList();
    }

    /// <summary>The pharmacy's date (Colombia), not the server's.</summary>
    private DateOnly Today => DateOnly.FromDateTime(clock.Now.DateTime);

    private async Task<Dictionary<Guid, string>> SupplierNamesAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken) =>
        (await suppliers.GetByIdsAsync(pharmacyId, supplierIds, cancellationToken)).ToDictionary(s => s.Id, s => s.Name);
}
