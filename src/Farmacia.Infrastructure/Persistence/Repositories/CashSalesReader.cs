using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// The cash a shift's sales left in the drawer: the cash payments' amount applied to each sale (what the customer
/// handed over minus the change), minus the cash refunded in that shift (returns of sales from any shift: the money
/// leaves this drawer). What a return credited to a customer's debt never left the drawer.
/// </summary>
internal sealed class CashSalesReader(FarmaciaDbContext db) : ICashSalesReader
{
    public async Task<decimal> GetNetCashAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken)
    {
        var received = await db.Sales
            .Where(s => s.PharmacyId == pharmacyId && s.CashSessionId == cashSessionId)
            .SelectMany(s => s.Payments)
            .Where(p => p.Method == PaymentMethod.Cash)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // CashRefund is computed (items' quantity × price, minus what went to the debt), so the sum is written over the
        // stored columns.
        var returns = db.SaleReturns.Where(r => r.PharmacyId == pharmacyId && r.CashSessionId == cashSessionId);
        var refunded = await returns
            .SelectMany(r => r.Items)
            .SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice), cancellationToken) ?? 0m;
        var creditedToDebt = await returns.SumAsync(r => (decimal?)r.CreditedToAccount, cancellationToken) ?? 0m;

        return received - (refunded - creditedToDebt);
    }
}
