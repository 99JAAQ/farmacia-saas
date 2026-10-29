using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;
using Farmacia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Api.Tests;

/// <summary>Each test sells in a pharmacy of its own: a pharmacy has a single register and its own ticket numbers.</summary>
[Collection(ApiCollection.Name)]
public class SalesEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> OpenShiftAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 100_000 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    private static object Cart(Guid productId, Guid boxId, int boxes, decimal cash, bool prescriptionChecked = false) => new
    {
        lines = new[] { new { productId, saleUnitId = boxId, quantity = boxes } },
        payments = new[] { new { method = "Cash", amount = cash } },
        prescriptionChecked,
    };

    [Fact]
    public async Task AWholeSale_OverHttp_WithMixedPayment_AndTheCashInTheShift()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90);
        var shiftId = await OpenShiftAsync(ana);

        // 2 boxes = 24.000: 10.000 by card, and 20.000 in cash for the remaining 14.000
        var response = await ana.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId, saleUnitId = boxId, quantity = 2 } },
            payments = new[] { new { method = "Card", amount = 10_000 }, new { method = "Cash", amount = 20_000 } },
            prescriptionChecked = false,
        });

        // The ticket comes in the answer, ready to print; Location points to it
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ticket = await JsonAsync(response);
        Assert.Equal(1, ticket.GetProperty("number").GetInt64());
        Assert.Equal(24_000m, ticket.GetProperty("total").GetDecimal());
        Assert.Equal(6_000m, ticket.GetProperty("change").GetDecimal());
        Assert.Equal("Usuario Cashier", ticket.GetProperty("soldByName").GetString());
        Assert.Equal(["Cash", "Card"], ticket.GetProperty("payments").EnumerateArray().Select(p => p.GetProperty("method").GetString()));
        Assert.DoesNotContain("cost", ticket.GetRawText(), StringComparison.OrdinalIgnoreCase); // margins stay out of the cashier's view
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync(response.Headers.Location)).StatusCode);

        // The shift's sales list
        var list = await JsonAsync(await ana.GetAsync($"/api/sales?cashSessionId={shiftId}"));
        var summary = Assert.Single(list.EnumerateArray());
        Assert.Equal(["Cash", "Card"], summary.GetProperty("methods").EnumerateArray().Select(m => m.GetString()));

        // Closing: 100.000 float + the 14.000 the cash paid (not the 20.000 handed over, nor the card)
        var closed = await JsonAsync(await ana.PostAsJsonAsync($"/api/cash-sessions/{shiftId}/close", new { countedCash = 114_000 }));
        Assert.Equal(114_000m, closed.GetProperty("expectedCash").GetDecimal());
        Assert.Equal(0m, closed.GetProperty("difference").GetDecimal());
    }

    [Fact]
    public async Task TheStockKeeper_CannotSell()
    {
        var stockKeeper = await api.LoggedInClientAsync(api.PharmacyA, Roles.StockKeeper);

        var response = await stockKeeper.PostAsJsonAsync("/api/sales", Cart(Guid.NewGuid(), Guid.NewGuid(), 1, 12_000));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Selling_NeedsAnOpenShift_OfYourOwn()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var beto = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90);

        var noShift = await ana.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000));
        Assert.Equal(HttpStatusCode.Conflict, noShift.StatusCode);

        await OpenShiftAsync(ana);
        var inAnasShift = await beto.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000));
        Assert.Equal(HttpStatusCode.Forbidden, inAnasShift.StatusCode);
    }

    [Fact]
    public async Task AProductThatNeedsAPrescription_IsSoldOnlyWithTheBoxChecked()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90, requiresPrescription: true);
        await OpenShiftAsync(ana);

        var notChecked = await ana.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000));
        Assert.Equal(HttpStatusCode.BadRequest, notChecked.StatusCode);
        Assert.Contains("fórmula", (await JsonAsync(notChecked)).GetProperty("detail").GetString());

        var isChecked = await ana.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000, prescriptionChecked: true));
        Assert.Equal(HttpStatusCode.Created, isChecked.StatusCode);
        Assert.True((await JsonAsync(isChecked)).GetProperty("prescriptionChecked").GetBoolean());
    }

    [Fact]
    public async Task ADoubleClick_OnTheLastBox_SellsItOnce()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 30);
        await OpenShiftAsync(ana);

        var responses = await Task.WhenAll(
            ana.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000)),
            ana.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000)));

        // The second one either collides with the first on the batch's RowVersion (409), or starts after the first
        // committed and finds no stock (400). Never two sales of one box.
        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Single(statuses, HttpStatusCode.Created);
        Assert.Single(statuses, s => s is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        Assert.Equal(1, await db.Sales.CountAsync(s => s.PharmacyId == pharmacyId));
        Assert.Equal(0, (await db.Batches.SingleAsync(b => b.ProductId == productId)).Quantity);
    }

    [Fact]
    public async Task ASaleOfAnotherPharmacy_IsNotFound_AndItsShiftShowsNoSales()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 30);
        var shiftId = await OpenShiftAsync(ana);
        var sale = await ana.PostAsJsonAsync("/api/sales", Cart(productId, boxId, 1, 12_000));
        var saleId = (await JsonAsync(sale)).GetProperty("id").GetString();
        var otherOwner = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);

        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync($"/api/sales/{saleId}")).StatusCode);
        Assert.Empty((await JsonAsync(await otherOwner.GetAsync($"/api/sales?cashSessionId={shiftId}"))).EnumerateArray());
    }
}
