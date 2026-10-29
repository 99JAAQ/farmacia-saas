using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// The cash a shift's sales put in the drawer: the cash payments' amount applied to each sale (what the customer
/// handed over minus the change). Refunds will subtract here when they exist.
/// </summary>
internal sealed class CashSalesReader(FarmaciaDbContext db) : ICashSalesReader
{
    public async Task<decimal> GetNetCashAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        await db.Sales
            .Where(s => s.PharmacyId == pharmacyId && s.CashSessionId == cashSessionId)
            .SelectMany(s => s.Payments)
            .Where(p => p.Method == PaymentMethod.Cash)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
}
