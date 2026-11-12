using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Dashboard;
using Farmacia.Domain.Common;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Tests.Dashboard;

public class DailySummaryCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Syrup = Guid.CreateVersion7();
    private static readonly Guid Tablets = Guid.CreateVersion7();
    private static readonly Guid SaleA = Guid.CreateVersion7();
    private static readonly Guid SaleB = Guid.CreateVersion7();

    private static readonly Dictionary<Guid, string> Names = new() { [Syrup] = "Jarabe", [Tablets] = "Acetaminofén" };

    [Fact]
    public void Sales_Vat_Cost_AndTheMargin_OfTheDay()
    {
        var day = new DailySalesData(
            [
                // Sale A at 9: a 19 % syrup and exempt tablets
                new DailySaleLine(SaleA, 9, Syrup, 1, 11_900m, VatRate.Standard, 6_000m),
                new DailySaleLine(SaleA, 9, Tablets, 30, 12_000m, VatRate.Exempt, 7_500m),
                // Sale B at 15: tablets only
                new DailySaleLine(SaleB, 15, Tablets, 60, 24_000m, VatRate.Exempt, 15_000m),
            ],
            [new DailyPayment(PaymentMethod.Cash, 23_900m), new DailyPayment(PaymentMethod.Card, 24_000m)],
            []);

        var summary = DailySummaryCalculator.Compute(Today, day, Names);

        Assert.Equal((2, 47_900m, 47_900m), (summary.SalesCount, summary.GrossSales, summary.NetSales));
        Assert.Equal(23_950m, summary.AverageTicket);
        Assert.Equal(1_900m, summary.Vat); // only the syrup's 19 %
        Assert.Equal(28_500m, summary.Cost);
        Assert.Equal(17_500m, summary.GrossProfit); // 47.900 − 1.900 − 28.500
        Assert.Equal(38.0m, summary.MarginPercent); // 17.500 / 46.000
        Assert.Equal([(9, 23_900m, 1), (15, 24_000m, 1)], summary.ByHour.Select(h => (h.Hour, h.Total, h.Sales)));
        Assert.Equal([PaymentMethod.Card, PaymentMethod.Cash], summary.ByPaymentMethod.Select(p => p.Method)); // largest first
        Assert.Equal([("Acetaminofén", 90), ("Jarabe", 1)], summary.TopProducts.Select(p => (p.Name, p.BaseUnits)));
    }

    [Fact]
    public void AReturn_TakesBackTheRevenueAndItsVat_ButOnlyRestockedUnitsGiveBackTheirCost()
    {
        var day = new DailySalesData(
            [new DailySaleLine(SaleA, 10, Syrup, 2, 23_800m, VatRate.Standard, 12_000m)],
            [new DailyPayment(PaymentMethod.Cash, 23_800m)],
            [
                new DailyReturnLine(11_900m, VatRate.Standard, RestockedCost: 6_000m), // back to the shelf
                new DailyReturnLine(11_900m, VatRate.Standard, RestockedCost: 0m), // broken: discarded, its cost is lost
            ]);

        var summary = DailySummaryCalculator.Compute(Today, day, Names);

        Assert.Equal((23_800m, 0m), (summary.Returns, summary.NetSales));
        Assert.Equal(0m, summary.Vat);
        Assert.Equal(6_000m, summary.Cost);
        Assert.Equal(-6_000m, summary.GrossProfit); // the discarded bottle
        Assert.Null(summary.MarginPercent); // no sales left to compare with
    }

    [Fact]
    public void AnEmptyDay_IsZeros_NotErrors()
    {
        var summary = DailySummaryCalculator.Compute(Today, new DailySalesData([], [], []), Names);

        Assert.Equal((0, 0m, 0m, 0m), (summary.SalesCount, summary.NetSales, summary.AverageTicket, summary.GrossProfit));
        Assert.Null(summary.MarginPercent);
        Assert.Empty(summary.ByHour);
    }
}
