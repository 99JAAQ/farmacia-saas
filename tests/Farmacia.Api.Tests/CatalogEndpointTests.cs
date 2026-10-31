using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

/// <summary>Keeping the catalog up to date over HTTP. Each test works in a pharmacy of its own.</summary>
[Collection(ApiCollection.Name)]
public class CatalogEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> CreateProductAsync(HttpClient client, string name = "Acetaminofén 500 mg", string? barcode = null)
    {
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            baseUnitName = "Tableta",
            requiresPrescription = false,
            saleUnits = new[] { new { name = "Caja x30", conversionFactor = 30, price = 12_000, barcode } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ProductAsync(HttpClient client, string id) =>
        await JsonAsync(await client.GetAsync($"/api/products/{id}"));

    [Fact]
    public async Task ThePriceAndThePresentations_CanChange_AndTheNextSaleUsesTheNewPrice()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var id = await CreateProductAsync(keeper);
        var boxId = (await ProductAsync(keeper, id)).GetProperty("saleUnits")[0].GetProperty("id").GetString();
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(id), "L1", new DateOnly(2030, 12, 31), 90);

        // A category, the corrected data, a new price, and loose tablets
        var category = await JsonAsync(await keeper.PostAsJsonAsync("/api/categories", new { name = "Analgésicos" }));
        var categoryId = category.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.PutAsJsonAsync($"/api/products/{id}", new
        {
            name = "Acetaminofén 500 mg tabletas", baseUnitName = "Tableta", requiresPrescription = false,
            activeIngredient = "Acetaminofén", categoryId, vatRate = "Exempt",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.PutAsJsonAsync($"/api/products/{id}/sale-units/{boxId}",
            new { name = "Caja x30", conversionFactor = 30, price = 13_000 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await keeper.PostAsJsonAsync($"/api/products/{id}/sale-units",
            new { name = "Tableta", conversionFactor = 1, price = 500 })).StatusCode);

        var product = await ProductAsync(keeper, id);
        Assert.Equal(("Acetaminofén 500 mg tabletas", categoryId), (product.GetProperty("name").GetString(), product.GetProperty("categoryId").GetString()));
        Assert.Equal([("Caja x30", 13_000m), ("Tableta", 500m)],
            product.GetProperty("saleUnits").EnumerateArray().Select(u => (u.GetProperty("name").GetString(), u.GetProperty("price").GetDecimal())));

        // The next sale charges 13.000
        Assert.Equal(HttpStatusCode.Created, (await cashier.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 0 })).StatusCode);
        var sale = await cashier.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId = id, saleUnitId = boxId, quantity = 1 } },
            payments = new[] { new { method = "Cash", amount = 13_000 } },
            prescriptionChecked = false,
        });
        Assert.Equal(13_000m, (await JsonAsync(sale)).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task ADiscontinuedProduct_LeavesThePos_IsNotSoldNorBought_AndCanComeBack()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var id = await CreateProductAsync(owner, "Jarabe descontinuado");
        var boxId = (await ProductAsync(owner, id)).GetProperty("saleUnits")[0].GetProperty("id").GetString();
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(id), "L1", new DateOnly(2030, 12, 31), 90);
        var (supplierId, _, _) = await api.SeedCatalogAsync(pharmacyId);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/products/{id}/deactivate", null)).StatusCode);

        // Not in the POS search, but in the catalog when asked
        Assert.Empty((await JsonAsync(await owner.GetAsync("/api/products?search=descontinuado"))).EnumerateArray());
        var listed = Assert.Single((await JsonAsync(await owner.GetAsync("/api/products/catalog?search=descontinuado&includeInactive=true"))).EnumerateArray());
        Assert.False(listed.GetProperty("product").GetProperty("isActive").GetBoolean());

        // Not sold...
        await owner.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 0 });
        var sale = await owner.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId = id, saleUnitId = boxId, quantity = 1 } },
            payments = new[] { new { method = "Cash", amount = 12_000 } },
            prescriptionChecked = false,
        });
        Assert.Equal(HttpStatusCode.BadRequest, sale.StatusCode);
        Assert.Contains("inactivo", (await JsonAsync(sale)).GetProperty("detail").GetString());

        // ...nor bought
        var draft = await owner.PostAsJsonAsync("/api/purchases",
            new { supplierId, invoiceNumber = "FE-77", invoiceDate = "2026-10-30", paymentTerm = "Cash" });
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var purchase = await JsonAsync(draft);
        var line = await owner.PostAsJsonAsync($"/api/purchases/{purchase.GetProperty("id").GetString()}/items", new
        {
            productId = id, saleUnitId = boxId, quantity = 1, bonusQuantity = 0, unitCost = 9_000, lineDiscount = 0,
            vatRate = "Exempt", lotNumber = "L9", expirationDate = "2030-01-31",
        });
        Assert.Equal(HttpStatusCode.BadRequest, line.StatusCode);
        Assert.Contains("inactivo", (await JsonAsync(line)).GetProperty("detail").GetString()); // not a malformed request

        // Back in the catalog
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/products/{id}/activate", null)).StatusCode);
        Assert.Single((await JsonAsync(await owner.GetAsync("/api/products?search=descontinuado"))).EnumerateArray());
    }

    [Fact]
    public async Task TheCatalog_ShowsSellableAndExpiredStock_AndTheBatchesWithTheirCost()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var id = await CreateProductAsync(keeper, "Loratadina 10 mg");
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(id), "VIEJO", new DateOnly(2026, 6, 30), 20); // already expired
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(id), "B", new DateOnly(2028, 3, 31), 40);
        await api.SeedBatchAsync(pharmacyId, Guid.Parse(id), "A", new DateOnly(2027, 1, 31), 30);

        var item = Assert.Single((await JsonAsync(await keeper.GetAsync("/api/products/catalog?search=loratadina"))).EnumerateArray());
        Assert.Equal((70, 20, "2027-01-31"),
            (item.GetProperty("sellableUnits").GetInt32(), item.GetProperty("expiredUnits").GetInt32(), item.GetProperty("nextExpiration").GetString()));

        var batches = (await JsonAsync(await keeper.GetAsync($"/api/products/{id}/batches"))).EnumerateArray().ToList();
        Assert.Equal(["VIEJO", "A", "B"], batches.Select(b => b.GetProperty("lotNumber").GetString())); // first to expire first
        Assert.True(batches[0].GetProperty("isExpired").GetBoolean());
        Assert.Equal(250m, batches[1].GetProperty("unitCost").GetDecimal());
    }

    [Fact]
    public async Task TheCashier_SearchesProducts_ButNeitherEditsNorSeesCosts()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var id = await CreateProductAsync(keeper);

        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/api/products?search=acetaminof")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/api/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/products/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync($"/api/products/{id}/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/api/products/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsJsonAsync("/api/categories", new { name = "Otra" })).StatusCode);
    }

    [Fact]
    public async Task ABarcodeOfAnotherProduct_OrARepeatedCategory_IsAConflict()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        await CreateProductAsync(keeper, "Ibuprofeno 400 mg", barcode: "7700000000111");
        var id = await CreateProductAsync(keeper, "Naproxeno 250 mg", barcode: "7700000000222");
        var boxId = (await ProductAsync(keeper, id)).GetProperty("saleUnits")[0].GetProperty("id").GetString();

        var taken = await keeper.PutAsJsonAsync($"/api/products/{id}/sale-units/{boxId}",
            new { name = "Caja x30", conversionFactor = 30, price = 12_000, barcode = "7700000000111" });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Contains("otro producto", (await JsonAsync(taken)).GetProperty("detail").GetString());

        // Keeping its own code is fine
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.PutAsJsonAsync($"/api/products/{id}/sale-units/{boxId}",
            new { name = "Caja x30", conversionFactor = 30, price = 12_500, barcode = "7700000000222" })).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await keeper.PostAsJsonAsync("/api/categories", new { name = "Antiinflamatorios" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await keeper.PostAsJsonAsync("/api/categories", new { name = "antiinflamatorios" })).StatusCode);
    }

    [Fact]
    public async Task AProductOfAnotherPharmacy_IsNotFound()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var id = await CreateProductAsync(await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper));
        var other = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/products/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/products/{id}/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/products/{id}", new
        {
            name = "X", baseUnitName = "Tableta", requiresPrescription = false, vatRate = "Exempt",
        })).StatusCode);
    }
}
