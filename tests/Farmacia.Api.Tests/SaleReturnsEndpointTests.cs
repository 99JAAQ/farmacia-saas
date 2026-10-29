using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

/// <summary>Each test works in a pharmacy of its own: a pharmacy has a single register and its own numbers.</summary>
[Collection(ApiCollection.Name)]
public class SaleReturnsEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> OpenShiftAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 100_000 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    /// <summary>Sells the given boxes (12.000 each) in cash; returns the ticket.</summary>
    private async Task<JsonElement> SellAsync(HttpClient client, Guid pharmacyId, int boxes)
    {
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90);
        var response = await client.PostAsJsonAsync("/api/sales", new
        {
            lines = new[] { new { productId, saleUnitId = boxId, quantity = boxes } },
            payments = new[] { new { method = "Cash", amount = boxes * 12_000 } },
            prescriptionChecked = false,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    private static object ReturnOf(JsonElement ticket, int boxes, string disposition = "Restock") => new
    {
        saleId = ticket.GetProperty("id").GetString(),
        reason = "Le formularon otro medicamento",
        lines = new[] { new { saleItemId = ticket.GetProperty("items")[0].GetProperty("id").GetString(), quantity = boxes, disposition } },
    };

    [Fact]
    public async Task AWholeReturn_OverHttp_FromTheTicketNumber_ToTheShiftsCash()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var shiftId = await OpenShiftAsync(ana);
        var sold = await SellAsync(ana, pharmacyId, boxes: 3);

        // 1. The cashier types the number printed on the customer's ticket
        var found = await ana.GetAsync($"/api/sales/by-number/{sold.GetProperty("number").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var ticket = await JsonAsync(found);

        // 2. Returns 1 of the 3 boxes; the disposition travels as text. The voucher comes in the answer
        var response = await ana.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var voucher = await JsonAsync(response);
        Assert.Equal(1, voucher.GetProperty("number").GetInt64());
        Assert.Equal(ticket.GetProperty("number").GetInt64(), voucher.GetProperty("saleNumber").GetInt64());
        Assert.Equal(12_000m, voucher.GetProperty("refund").GetDecimal());
        Assert.Equal("Restock", voucher.GetProperty("items")[0].GetProperty("disposition").GetString());
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync(response.Headers.Location)).StatusCode);

        // 3. The ticket now shows what was returned
        var again = await JsonAsync(await ana.GetAsync($"/api/sales/{ticket.GetProperty("id").GetString()}"));
        Assert.Equal(1, again.GetProperty("items")[0].GetProperty("returnedQuantity").GetInt32());

        // 4. Closing: 100.000 float + 36.000 sold − 12.000 refunded
        var closed = await JsonAsync(await ana.PostAsJsonAsync($"/api/cash-sessions/{shiftId}/close", new { countedCash = 124_000 }));
        Assert.Equal(124_000m, closed.GetProperty("expectedCash").GetDecimal());
    }

    [Fact]
    public async Task ReturningMoreThanIsLeft_IsABadRequest_ThatSaysHowManyAreLeft()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        await OpenShiftAsync(ana);
        var ticket = await SellAsync(ana, pharmacyId, boxes: 2);
        Assert.Equal(HttpStatusCode.Created, (await ana.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1, "Discard"))).StatusCode);

        var tooMany = await ana.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 2));

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Contains("solo quedan 1", (await JsonAsync(tooMany)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Returning_NeedsAnOpenShift_OfYourOwn_AndIsNotForTheStockKeeper()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var beto = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var stockKeeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var shiftId = await OpenShiftAsync(ana);
        var ticket = await SellAsync(ana, pharmacyId, boxes: 1);

        Assert.Equal(HttpStatusCode.Forbidden, (await beto.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stockKeeper.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1))).StatusCode);

        await ana.PostAsJsonAsync($"/api/cash-sessions/{shiftId}/close", new { countedCash = 112_000 });
        Assert.Equal(HttpStatusCode.Conflict, (await ana.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1))).StatusCode);
    }

    [Fact]
    public async Task TicketsAndReturnsOfAnotherPharmacy_AreNotFound()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        await OpenShiftAsync(ana);
        var ticket = await SellAsync(ana, pharmacyId, boxes: 2);
        var voucher = await JsonAsync(await ana.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1)));

        var otherPharmacy = await api.CreatePharmacyAsync();
        var other = await api.LoggedInClientAsync(otherPharmacy, Roles.Cashier);
        await OpenShiftAsync(other);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sales/by-number/{ticket.GetProperty("number").GetInt64()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/sale-returns/{voucher.GetProperty("id").GetString()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/sale-returns", ReturnOf(ticket, 1))).StatusCode);
    }
}
