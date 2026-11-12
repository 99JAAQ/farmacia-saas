using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class DashboardEndpointTests(ApiFactory api)
{
    [Fact]
    public async Task TheOwner_SeesTheDaysSalesAndMargin_OfTheirPharmacyOnly()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var otherOwner = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90); // costs 250 per tablet

        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 0 })).StatusCode);
        var sale = await owner.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId, saleUnitId = boxId, quantity = 2 } },
            payments = new[] { new { method = "Cash", amount = 24_000 } },
            prescriptionChecked = false,
        });
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        var today = await owner.GetFromJsonAsync<JsonElement>("/api/dashboard/today");
        Assert.Equal(1, today.GetProperty("salesCount").GetInt32());
        Assert.Equal(24_000m, today.GetProperty("netSales").GetDecimal());
        Assert.Equal(15_000m, today.GetProperty("cost").GetDecimal()); // 60 tablets × 250
        Assert.Equal(9_000m, today.GetProperty("grossProfit").GetDecimal()); // exempt: no VAT
        Assert.Equal("Cash", today.GetProperty("byPaymentMethod")[0].GetProperty("method").GetString());

        var other = await otherOwner.GetFromJsonAsync<JsonElement>("/api/dashboard/today");
        Assert.Equal(0, other.GetProperty("salesCount").GetInt32());
    }

    [Fact]
    public async Task TheMargin_IsTheOwnersOnly()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        foreach (var role in new[] { Roles.Cashier, Roles.StockKeeper })
        {
            var client = await api.LoggedInClientAsync(pharmacyId, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dashboard/today")).StatusCode);
        }
    }
}
