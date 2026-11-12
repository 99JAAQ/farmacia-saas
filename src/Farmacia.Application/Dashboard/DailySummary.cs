using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Common;
using Farmacia.Domain.Sales;
using MediatR;

namespace Farmacia.Application.Dashboard;

/// <summary>How the day is going, for the owner: it shows costs and margin, so it is not for the cashier.</summary>
public sealed record GetDailySummaryQuery : IRequest<DailySummaryDto>;

/// <param name="NetSales">What was sold minus what was refunded today, VAT included (what came in).</param>
/// <param name="GrossProfit">Net sales without VAT, minus what those units cost (the batches' real cost).</param>
public sealed record DailySummaryDto(
    DateOnly Date,
    int SalesCount,
    decimal GrossSales,
    decimal Returns,
    decimal NetSales,
    decimal AverageTicket,
    decimal Vat,
    decimal Cost,
    decimal GrossProfit,
    decimal? MarginPercent,
    IReadOnlyList<PaymentMethodTotalDto> ByPaymentMethod,
    IReadOnlyList<HourlySalesDto> ByHour,
    IReadOnlyList<TopProductDto> TopProducts);

public sealed record PaymentMethodTotalDto(PaymentMethod Method, decimal Amount, int Payments);

public sealed record HourlySalesDto(int Hour, decimal Total, int Sales);

/// <param name="BaseUnits">Units sold in the product's base unit (tablets, not boxes): comparable across presentations.</param>
public sealed record TopProductDto(Guid ProductId, string Name, int BaseUnits, decimal Total);

public sealed class GetDailySummaryHandler(
    IDailySalesData data,
    IProductRepository products,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<GetDailySummaryQuery, DailySummaryDto>
{
    public async Task<DailySummaryDto> Handle(GetDailySummaryQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        // The pharmacy's day: midnight to midnight in Colombia, whatever the server's time zone.
        var today = DateOnly.FromDateTime(clock.Now.DateTime);
        var day = await data.GetAsync(pharmacyId, today, cancellationToken);

        var topIds = DailySummaryCalculator.TopProductIds(day.Lines, 5);
        var names = (await products.GetByIdsAsync(pharmacyId, topIds, cancellationToken)).ToDictionary(p => p.Id, p => p.Name);

        return DailySummaryCalculator.Compute(today, day, names);
    }
}

/// <summary>The arithmetic of the summary, apart from the database so it can be tested line by line.</summary>
public static class DailySummaryCalculator
{
    public static IReadOnlyList<Guid> TopProductIds(IReadOnlyList<DailySaleLine> lines, int count) =>
        lines.GroupBy(l => l.ProductId)
            .OrderByDescending(g => g.Sum(l => l.Total))
            .Take(count)
            .Select(g => g.Key)
            .ToList();

    public static DailySummaryDto Compute(DateOnly date, DailySalesData day, IReadOnlyDictionary<Guid, string> productNames)
    {
        var salesCount = day.Lines.Select(l => l.SaleId).Distinct().Count();
        var grossSales = day.Lines.Sum(l => l.Total);
        var returns = day.Returns.Sum(r => r.Refund);
        var netSales = grossSales - returns;

        // VAT is included in prices: it is the government's, not part of the margin. Rounded per line, like the ticket.
        var vat = day.Lines.Sum(l => l.VatRate.IncludedIn(l.Total)) - day.Returns.Sum(r => r.VatRate.IncludedIn(r.Refund));
        // Returned units that went back to a batch recover their cost; discarded ones are a loss (their cost stays).
        var cost = Round(day.Lines.Sum(l => l.Cost) - day.Returns.Sum(r => r.RestockedCost));
        var grossProfit = netSales - vat - cost;
        var withoutVat = netSales - vat;

        return new DailySummaryDto(
            date,
            salesCount,
            grossSales,
            returns,
            netSales,
            salesCount == 0 ? 0 : Round(grossSales / salesCount),
            vat,
            cost,
            grossProfit,
            withoutVat > 0 ? Math.Round(grossProfit / withoutVat * 100, 1, MidpointRounding.AwayFromZero) : null,
            day.Payments
                .GroupBy(p => p.Method)
                .Select(g => new PaymentMethodTotalDto(g.Key, g.Sum(p => p.Amount), g.Count()))
                .OrderByDescending(p => p.Amount)
                .ToList(),
            day.Lines
                .GroupBy(l => l.Hour)
                .Select(g => new HourlySalesDto(g.Key, g.Sum(l => l.Total), g.Select(l => l.SaleId).Distinct().Count()))
                .OrderBy(h => h.Hour)
                .ToList(),
            TopProductIds(day.Lines, 5)
                .Select(id =>
                {
                    var lines = day.Lines.Where(l => l.ProductId == id).ToList();
                    return new TopProductDto(id, productNames.GetValueOrDefault(id, "Producto"), lines.Sum(l => l.BaseUnits), lines.Sum(l => l.Total));
                })
                .ToList());
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
