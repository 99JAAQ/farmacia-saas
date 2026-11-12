using Farmacia.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;


namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// The day's rows, read without tracking. A pharmacy's day is a few hundred lines, so the arithmetic is done in
/// Application (DailySummaryCalculator), with the domain's VAT rule, instead of in SQL.
/// </summary>
internal sealed class DailySalesReader(FarmaciaDbContext db) : IDailySalesData
{
    // Colombia has no daylight saving time: its day always starts at 00:00 -05:00 (same as ColombiaClock).
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public async Task<DailySalesData> GetAsync(Guid pharmacyId, DateOnly date, CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Colombia);
        var to = from.AddDays(1);

        var sales = db.Sales.AsNoTracking().Where(s => s.PharmacyId == pharmacyId && s.SoldAt >= from && s.SoldAt < to);

        var lines = await sales
            .SelectMany(s => s.Items, (s, i) => new
            {
                s.Id,
                s.SoldAt,
                i.ProductId,
                BaseUnits = i.Quantity * i.ConversionFactor,
                Total = i.Quantity * i.UnitPrice,
                i.VatRate,
                Cost = i.Batches.Sum(b => (decimal?)(b.Quantity * b.UnitCost)) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var payments = await sales
            .SelectMany(s => s.Payments)
            .Select(p => new DailyPayment(p.Method, p.Amount))
            .ToListAsync(cancellationToken);

        // Returns made today, of sales from any day: the money left the business today.
        var returns = await db.SaleReturns.AsNoTracking()
            .Where(r => r.PharmacyId == pharmacyId && r.ReturnedAt >= from && r.ReturnedAt < to)
            .SelectMany(r => r.Items)
            .Select(i => new
            {
                Refund = i.Quantity * i.UnitPrice,
                VatRate = db.Sales.SelectMany(s => s.Items).Where(si => si.Id == i.SaleItemId).Select(si => si.VatRate).First(),
                RestockedCost = i.Batches.Sum(b => (decimal?)(b.Quantity * b.UnitCost)) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        return new DailySalesData(
            // The stored offset is Colombia's, but convert anyway: the hour must be the pharmacy's, not the server's.
            lines.Select(l => new DailySaleLine(l.Id, l.SoldAt.ToOffset(Colombia).Hour, l.ProductId, l.BaseUnits, l.Total, l.VatRate, l.Cost)).ToList(),
            payments,
            returns.Select(r => new DailyReturnLine(r.Refund, r.VatRate, r.RestockedCost)).ToList());
    }
}
