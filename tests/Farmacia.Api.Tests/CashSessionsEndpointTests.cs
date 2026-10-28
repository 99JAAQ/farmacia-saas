using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

/// <summary>Each test opens shifts in a pharmacy of its own: a pharmacy has a single register.</summary>
[Collection(ApiCollection.Name)]
public class CashSessionsEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> CurrentAsync(HttpClient client) =>
        await JsonAsync(await client.GetAsync("/api/cash-sessions/current"));

    private static async Task<string> OpenAsync(HttpClient client, decimal openingFloat = 100_000m)
    {
        var response = await client.PostAsJsonAsync("/api/cash-sessions", new { openingFloat });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task TheRegister_IsNotForTheStockKeeper()
    {
        var stockKeeper = await api.LoggedInClientAsync(api.PharmacyA, Roles.StockKeeper);

        var response = await stockKeeper.GetAsync("/api/cash-sessions/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AWholeShift_OverHttp_WithBlindCount()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var beto = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);

        // 1. Ana opens; the Location header points to the shift
        var created = await ana.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 100_000 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync(created.Headers.Location)).StatusCode);
        var id = (await JsonAsync(created)).GetProperty("id").GetString();

        // 2. One register: Beto cannot open another one
        var second = await beto.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 50_000 });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("Usuario Cashier", (await JsonAsync(second)).GetProperty("detail").GetString());

        // 3. A delivery paid from the drawer; the enum travels as text
        var expense = await ana.PostAsJsonAsync("/api/cash-sessions/current/movements",
            new { type = "Expense", amount = 8_000, reason = "Domicilio" });
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);

        // 4. ...but not from Beto's hands: it is Ana's shift
        var foreign = await beto.PostAsJsonAsync("/api/cash-sessions/current/movements",
            new { type = "Income", amount = 1_000, reason = "Sencillo" });
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal("application/problem+json", foreign.Content.Headers.ContentType?.MediaType);

        // 5. Blind count: the HTTP answer itself hides the expected cash from the cashier, not just the screen
        var asAna = (await CurrentAsync(ana)).GetProperty("session");
        Assert.Equal("Open", asAna.GetProperty("status").GetString());
        Assert.True(asAna.GetProperty("isMine").GetBoolean());
        Assert.Equal(JsonValueKind.Null, asAna.GetProperty("expectedCash").ValueKind);
        Assert.Equal("Expense", asAna.GetProperty("movements")[0].GetProperty("type").GetString());

        var asOwner = (await CurrentAsync(owner)).GetProperty("session");
        Assert.Equal(92_000m, asOwner.GetProperty("expectedCash").GetDecimal());

        // 6. Ana counts and closes: now she sees the expected cash and the difference
        var closed = await ana.PostAsJsonAsync($"/api/cash-sessions/{id}/close", new { countedCash = 90_000, notes = "Faltan 2.000" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var result = await JsonAsync(closed);
        Assert.Equal("Closed", result.GetProperty("status").GetString());
        Assert.Equal(92_000m, result.GetProperty("expectedCash").GetDecimal());
        Assert.Equal(-2_000m, result.GetProperty("difference").GetDecimal());

        // 7. The register is free, and the next float suggested is what Ana left
        var current = await CurrentAsync(beto);
        Assert.Equal(JsonValueKind.Null, current.GetProperty("session").ValueKind);
        Assert.Equal(90_000m, current.GetProperty("suggestedOpeningFloat").GetDecimal());
    }

    [Fact]
    public async Task AnotherCashier_CannotClose_ButTheOwnerCanCloseAForgottenShift()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var beto = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var id = await OpenAsync(ana);

        var byBeto = await beto.PostAsJsonAsync($"/api/cash-sessions/{id}/close", new { countedCash = 100_000 });
        Assert.Equal(HttpStatusCode.Forbidden, byBeto.StatusCode);

        var byOwner = await owner.PostAsJsonAsync($"/api/cash-sessions/{id}/close", new { countedCash = 100_000 });
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        var closed = await JsonAsync(byOwner);
        Assert.Equal("Usuario Cashier", closed.GetProperty("openedByName").GetString());
        Assert.Equal("Usuario Owner", closed.GetProperty("closedByName").GetString());
    }

    [Fact]
    public async Task AShiftOfAnotherPharmacy_IsNotFound_NotForbidden()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var id = await OpenAsync(await api.LoggedInClientAsync(pharmacyId, Roles.Cashier));
        var otherOwner = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);

        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync($"/api/cash-sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherOwner.PostAsJsonAsync($"/api/cash-sessions/{id}/close", new { countedCash = 0 })).StatusCode);
    }
}
