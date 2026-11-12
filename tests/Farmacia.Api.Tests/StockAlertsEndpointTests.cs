using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class StockAlertsEndpointTests(ApiFactory api)
{
    [Fact]
    public async Task ASaleThatLeavesTheProductBelowItsMinimum_PutsItInTheAlerts_OfItsPharmacyOnly()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var stranger = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90); // 3 boxes

        // Minimum: 2 boxes = 60 tablets (the screen converts; the API takes base units).
        Assert.Equal(HttpStatusCode.NoContent,
            (await keeper.PutAsJsonAsync($"/api/products/{productId}/minimum-stock", new { minimumStock = 60 })).StatusCode);
        Assert.Equal(0, (await AlertsAsync(keeper)).GetProperty("lowStock").GetArrayLength());

        await owner.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 0 });
        var sale = await owner.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId, saleUnitId = boxId, quantity = 2 } },
            payments = new[] { new { method = "Cash", amount = 24_000 } },
            prescriptionChecked = false,
        });
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        var low = Assert.Single((await AlertsAsync(keeper)).GetProperty("lowStock").EnumerateArray());
        Assert.Equal(productId, low.GetProperty("product").GetProperty("id").GetGuid());
        Assert.Equal((30, 60), (low.GetProperty("sellableUnits").GetInt32(), low.GetProperty("minimumStock").GetInt32()));
        Assert.Equal(60, low.GetProperty("product").GetProperty("minimumStock").GetInt32());

        Assert.Equal(0, (await AlertsAsync(stranger)).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task AnExpiredBatch_IsAnAlertToWriteOff_AndTheCashierDoesNotSeeAlerts()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, _) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 30);
        await api.SeedBatchAsync(pharmacyId, productId, "VENCIDO", new DateOnly(2026, 1, 31), 12);

        var expired = Assert.Single((await AlertsAsync(keeper)).GetProperty("expired").EnumerateArray());
        Assert.Equal(("VENCIDO", 12), (expired.GetProperty("lotNumber").GetString(), expired.GetProperty("quantity").GetInt32()));

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/stock-alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.PutAsJsonAsync($"/api/products/{productId}/minimum-stock", new { minimumStock = 10 })).StatusCode);
    }

    private static Task<JsonElement> AlertsAsync(HttpClient client) => client.GetFromJsonAsync<JsonElement>("/api/stock-alerts");
}
