using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

/// <summary>Write-offs, physical counts and the kardex over HTTP. Each test works in a pharmacy of its own.</summary>
[Collection(ApiCollection.Name)]
public class InventoryEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<(string ProductId, string BoxId)> CreateProductAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Loratadina 10 mg",
            baseUnitName = "Tableta",
            requiresPrescription = false,
            saleUnits = new[] { new { name = "Caja x10", conversionFactor = 10, price = 6_000 } },
        });
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;
        var product = await JsonAsync(await client.GetAsync($"/api/products/{id}"));
        return (id, product.GetProperty("saleUnits")[0].GetProperty("id").GetString()!);
    }

    private static async Task<JsonElement[]> BatchesAsync(HttpClient client, string productId) =>
        (await JsonAsync(await client.GetAsync($"/api/products/{productId}/batches"))).EnumerateArray().ToArray();

    [Fact]
    public async Task WriteOffAndCount_LeaveTheKardexExplainingTheStock_WithWhoAndWhy()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await CreateProductAsync(keeper);
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(productId), "VIEJO", new DateOnly(2026, 6, 30), 20); // expired
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(productId), "BUENO", new DateOnly(2028, 1, 31), 50);
        var batches = await BatchesAsync(keeper, productId);
        var expiredId = batches.Single(b => b.GetProperty("lotNumber").GetString() == "VIEJO").GetProperty("id").GetString();
        var goodId = batches.Single(b => b.GetProperty("lotNumber").GetString() == "BUENO").GetProperty("id").GetString();

        // 1. A sale of 1 box (10 tablets) from the good batch, by the cashier
        await cashier.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 0 });
        var sale = await JsonAsync(await cashier.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId, saleUnitId = boxId, quantity = 1 } },
            payments = new[] { new { method = "Cash", amount = 6_000 } },
            prescriptionChecked = false,
        }));

        // 2. The stock keeper writes off the expired batch, and counts 38 of the 40 left (2 damaged)
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.PostAsync($"/api/batches/{expiredId}/write-off", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.PostAsJsonAsync($"/api/batches/{goodId}/adjust",
            new { countedQuantity = 38, reason = "Dos tabletas dañadas" })).StatusCode);

        // 3. The kardex, newest first: adjustment, write-off, sale (and the two seeded receptions)
        var kardex = (await JsonAsync(await keeper.GetAsync($"/api/products/{productId}/kardex"))).EnumerateArray().ToArray();
        Assert.Equal(["Adjustment", "ExpiredWriteOff", "Sale"], kardex.Take(3).Select(e => e.GetProperty("type").GetString()));
        Assert.Equal([38, 40, 60], kardex.Take(3).Select(e => e.GetProperty("productBalanceAfter").GetInt32()));

        var adjustment = kardex[0];
        Assert.Equal(("Dos tabletas dañadas", "Usuario StockKeeper", -2),
            (adjustment.GetProperty("notes").GetString(), adjustment.GetProperty("performedByName").GetString(), adjustment.GetProperty("quantityChange").GetInt32()));
        Assert.Equal($"Venta N.º {sale.GetProperty("number").GetInt64()}", kardex[2].GetProperty("document").GetString());
        Assert.Equal(("Usuario Cashier", "BUENO"), (kardex[2].GetProperty("performedByName").GetString(), kardex[2].GetProperty("lotNumber").GetString()));

        // 4. The catalog agrees: nothing expired is left, 38 sellable
        var item = Assert.Single((await JsonAsync(await keeper.GetAsync("/api/products/catalog?search=loratadina"))).EnumerateArray());
        Assert.Equal((38, 0), (item.GetProperty("sellableUnits").GetInt32(), item.GetProperty("expiredUnits").GetInt32()));
    }

    [Fact]
    public async Task ABatchThatHasNotExpired_IsNotWrittenOff_AndACountNeedsAReason()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var (productId, _) = await CreateProductAsync(keeper);
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(productId), "BUENO", new DateOnly(2028, 1, 31), 50);
        var batchId = (await BatchesAsync(keeper, productId))[0].GetProperty("id").GetString();

        var writeOff = await keeper.PostAsync($"/api/batches/{batchId}/write-off", null);
        Assert.Equal(HttpStatusCode.BadRequest, writeOff.StatusCode);
        Assert.Contains("no está vencido", (await JsonAsync(writeOff)).GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await keeper.PostAsJsonAsync($"/api/batches/{batchId}/adjust", new { countedQuantity = 45, reason = "" })).StatusCode);
        Assert.Equal(50, (await BatchesAsync(keeper, productId))[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task TheCashier_TouchesNoInventory_AndAnotherPharmacysBatch_IsNotFound()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, _) = await CreateProductAsync(keeper);
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(productId), "VIEJO", new DateOnly(2026, 6, 30), 20);
        var batchId = (await BatchesAsync(keeper, productId))[0].GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/api/batches/{batchId}/write-off", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/products/{productId}/kardex")).StatusCode);

        var otherOwner = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.PostAsync($"/api/batches/{batchId}/write-off", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync($"/api/products/{productId}/kardex")).StatusCode);
        Assert.Equal(20, (await BatchesAsync(keeper, productId))[0].GetProperty("quantity").GetInt32());
    }
}
