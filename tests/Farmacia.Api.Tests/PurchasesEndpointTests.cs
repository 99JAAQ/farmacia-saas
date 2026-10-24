using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class PurchasesEndpointTests(ApiFactory api)
{
    private static string NewInvoiceNumber() => $"FE-{Random.Shared.Next(1_000_000, 9_999_999)}";

    private static object Header(Guid supplierId, string? invoiceNumber = null) => new
    {
        supplierId,
        invoiceNumber = invoiceNumber ?? NewInvoiceNumber(),
        invoiceDate = "2026-10-01",
        paymentTerm = "Credit",
        dueDate = "2026-10-31",
    };

    // 10 boxes x30 at 9.000, 2 bonus boxes, 3.000 discount: 87.000 for 360 tablets.
    private static object Line(Guid productId, Guid saleUnitId) => new
    {
        productId,
        saleUnitId,
        quantity = 10,
        bonusQuantity = 2,
        unitCost = 9_000,
        lineDiscount = 3_000,
        vatRate = "Exempt",
        lotNumber = "L1",
        expirationDate = "2027-12-31",
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Purchases_AreNotVisibleToTheCashier_BecauseTheyShowCosts()
    {
        var cashier = await api.LoggedInClientAsync(api.PharmacyA, Roles.Cashier);

        var response = await cashier.GetAsync("/api/purchases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheWholeReception_OverHttp()
    {
        var (supplierId, productId, boxId) = await api.SeedCatalogAsync(api.PharmacyA);
        var stockKeeper = await api.LoggedInClientAsync(api.PharmacyA, Roles.StockKeeper);

        // 1. Draft
        var created = await stockKeeper.PostAsJsonAsync("/api/purchases", Header(supplierId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var purchaseUrl = created.Headers.Location!.ToString();

        // 2. One line, saved right away
        var line = await stockKeeper.PostAsJsonAsync($"{purchaseUrl}/items", Line(productId, boxId));
        Assert.Equal(HttpStatusCode.Created, line.StatusCode);

        // 3. Totals while typing; enums travel as text
        var draft = await JsonAsync(await stockKeeper.GetAsync(purchaseUrl));
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal("Credit", draft.GetProperty("paymentTerm").GetString());
        Assert.Equal(87_000m, draft.GetProperty("totals").GetProperty("total").GetDecimal());

        // 4. A typo in the printed total is caught
        var wrongTotal = await stockKeeper.PostAsJsonAsync($"{purchaseUrl}/confirm", new { printedInvoiceTotal = 90_000 });
        Assert.Equal(HttpStatusCode.BadRequest, wrongTotal.StatusCode);
        Assert.Contains("diferencia de $3.000", (await JsonAsync(wrongTotal)).GetProperty("detail").GetString());

        // 5. Confirm
        var confirmed = await stockKeeper.PostAsJsonAsync($"{purchaseUrl}/confirm", new { printedInvoiceTotal = 87_000 });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var purchase = await JsonAsync(confirmed);
        Assert.Equal("Confirmed", purchase.GetProperty("status").GetString());
        var item = Assert.Single(purchase.GetProperty("items").EnumerateArray());
        Assert.Equal(241.6667m, item.GetProperty("unitCostPerBaseUnit").GetDecimal());
        Assert.Equal(JsonValueKind.String, item.GetProperty("batchId").ValueKind);

        // 6. Locked
        var afterConfirm = await stockKeeper.PostAsJsonAsync($"{purchaseUrl}/items", Line(productId, boxId));
        Assert.Equal(HttpStatusCode.BadRequest, afterConfirm.StatusCode);
    }

    [Fact]
    public async Task TheSameInvoiceTwice_IsAConflict()
    {
        var (supplierId, _, _) = await api.SeedCatalogAsync(api.PharmacyA);
        var owner = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);
        var invoiceNumber = NewInvoiceNumber();

        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync("/api/purchases", Header(supplierId, invoiceNumber))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/purchases", Header(supplierId, invoiceNumber.ToLowerInvariant()))).StatusCode);
    }

    [Fact]
    public async Task APurchaseOfAnotherPharmacy_IsNotFound()
    {
        var (supplierId, _, _) = await api.SeedCatalogAsync(api.PharmacyA);
        var ownerA = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);
        var ownerB = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);
        var created = await ownerA.PostAsJsonAsync("/api/purchases", Header(supplierId));

        var response = await ownerB.GetAsync(created.Headers.Location);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ADraftCanBeResumed_FromTheListOfDrafts_AndALineRemoved()
    {
        var (supplierId, productId, boxId) = await api.SeedCatalogAsync(api.PharmacyB);
        var owner = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);
        var created = await owner.PostAsJsonAsync("/api/purchases", Header(supplierId));
        var purchaseId = (await JsonAsync(created)).GetProperty("id").GetString();
        var lineId = (await JsonAsync(await owner.PostAsJsonAsync($"/api/purchases/{purchaseId}/items", Line(productId, boxId))))
            .GetProperty("id").GetString();

        // After a power cut: the draft is still there.
        var drafts = await owner.GetFromJsonAsync<JsonElement>("/api/purchases?status=Draft");
        Assert.Contains(drafts.EnumerateArray(), d => d.GetProperty("id").GetString() == purchaseId);

        var removed = await owner.DeleteAsync($"/api/purchases/{purchaseId}/items/{lineId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var purchase = await owner.GetFromJsonAsync<JsonElement>($"/api/purchases/{purchaseId}");
        Assert.Empty(purchase.GetProperty("items").EnumerateArray());
    }
}
