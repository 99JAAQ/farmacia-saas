using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

/// <summary>Credit ("fiado") over HTTP. Each test works in a pharmacy of its own: one register, its own numbers.</summary>
[Collection(ApiCollection.Name)]
public class CreditEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string> OpenShiftAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 100_000 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    /// <summary>The owner registers the customer and enables credit with the given limit.</summary>
    private static async Task<string> CustomerWithCreditAsync(HttpClient owner, decimal limit, string document = "41234567")
    {
        var created = await owner.PostAsJsonAsync("/api/customers",
            new { fullName = "Doña Rosa", documentType = "CitizenshipCard", documentNumber = document, phone = "3001234567" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;

        var opened = await owner.PostAsJsonAsync($"/api/customers/{id}/credit", new { limit });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        return id;
    }

    private static object SaleOnCredit(Guid productId, Guid boxId, int boxes, string customerId, decimal cash = 0) => new
    {
        lines = new[] { new { productId, saleUnitId = boxId, quantity = boxes } },
        payments = cash > 0
            ? new object[] { new { method = "Cash", amount = cash }, new { method = "Credit", amount = boxes * 12_000 - cash } }
            : [new { method = "Credit", amount = boxes * 12_000 }],
        prescriptionChecked = false,
        customerId,
    };

    [Fact]
    public async Task TheWholeCredit_OverHttp_FromTheOwnersDecision_ToTheShiftsCash()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90);
        var customerId = await CustomerWithCreditAsync(owner, limit: 50_000);
        var shiftId = await OpenShiftAsync(ana);

        // 1. The cashier finds the customer by document and sees what is available
        var found = Assert.Single((await JsonAsync(await ana.GetAsync("/api/customers?search=41234567"))).EnumerateArray());
        Assert.Equal(50_000m, found.GetProperty("credit").GetProperty("available").GetDecimal());

        // 2. Sells 2 boxes: 10.000 in cash and 14.000 on credit; the ticket names the customer
        var sale = await ana.PostAsJsonAsync("/api/sales", SaleOnCredit(productId, boxId, 2, customerId, cash: 10_000));
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
        var ticket = await JsonAsync(sale);
        Assert.Equal(("Doña Rosa", 14_000m), (ticket.GetProperty("customerName").GetString(), ticket.GetProperty("creditAmount").GetDecimal()));

        // 3. Days later the customer pays 5.000 in cash: the voucher says what is still owed
        var paid = await ana.PostAsJsonAsync("/api/credit-payments", new { customerId, method = "Cash", amount = 5_000 });
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        var voucher = await JsonAsync(paid);
        Assert.Equal((1, 9_000m), (voucher.GetProperty("number").GetInt64(), voucher.GetProperty("balanceAfter").GetDecimal()));
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync(paid.Headers.Location)).StatusCode);

        // 4. The statement, newest first; the entry type travels as text
        var statement = await JsonAsync(await ana.GetAsync($"/api/customers/{customerId}/credit"));
        Assert.Equal(9_000m, statement.GetProperty("account").GetProperty("balance").GetDecimal());
        Assert.Equal(["Payment", "Sale"], statement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("type").GetString()));

        // 5. The owner's list of who owes
        var debtor = Assert.Single((await JsonAsync(await owner.GetAsync("/api/customers/debtors"))).EnumerateArray());
        Assert.Equal((customerId, 0), (debtor.GetProperty("customerId").GetString(), debtor.GetProperty("account").GetProperty("daysOwing").GetInt32()));

        // 6. Closing: 100.000 + 10.000 of the sale + 5.000 of the payment (the credit part never entered the drawer)
        var closed = await JsonAsync(await ana.PostAsJsonAsync($"/api/cash-sessions/{shiftId}/close", new { countedCash = 115_000 }));
        Assert.Equal(115_000m, closed.GetProperty("expectedCash").GetDecimal());
    }

    [Fact]
    public async Task OnlyTheOwner_DecidesWhoGetsCreditAndHowMuch()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var stockKeeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var customerId = await CustomerWithCreditAsync(owner, limit: 50_000);

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsJsonAsync("/api/customers",
            new { fullName = "Otro", documentType = "CitizenshipCard", documentNumber = "1000001" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync($"/api/customers/{customerId}/credit/limit", new { limit = 1_000_000 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/api/customers/{customerId}/credit/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/customers/debtors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stockKeeper.GetAsync("/api/customers")).StatusCode);

        // ...and the owner can
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/customers/{customerId}/credit/limit", new { limit = 80_000 })).StatusCode);
        var credit = (await JsonAsync(await cashier.GetAsync($"/api/customers/{customerId}"))).GetProperty("credit");
        Assert.Equal(80_000m, credit.GetProperty("limit").GetDecimal());
    }

    [Fact]
    public async Task TheLimit_ASuspension_AndACustomerWithoutCredit_StopTheSale()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var ana = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 300);
        var customerId = await CustomerWithCreditAsync(owner, limit: 30_000);
        await OpenShiftAsync(ana);

        var overLimit = await ana.PostAsJsonAsync("/api/sales", SaleOnCredit(productId, boxId, 3, customerId)); // 36.000
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
        Assert.Contains("disponibles", (await JsonAsync(overLimit)).GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/customers/{customerId}/credit/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync("/api/sales", SaleOnCredit(productId, boxId, 1, customerId))).StatusCode);

        var plain = await owner.PostAsJsonAsync("/api/customers",
            new { fullName = "Cliente nuevo", documentType = "CitizenshipCard", documentNumber = "1000002" });
        var plainId = (await JsonAsync(plain)).GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync("/api/sales", SaleOnCredit(productId, boxId, 1, plainId))).StatusCode);
    }

    [Fact]
    public async Task ARepeatedDocument_OrASecondAccount_IsAConflict()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var customerId = await CustomerWithCreditAsync(owner, limit: 30_000);

        var repeated = await owner.PostAsJsonAsync("/api/customers",
            new { fullName = "Rosa", documentType = "CitizenshipCard", documentNumber = "41234567" });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/customers/{customerId}/credit", new { limit = 10_000 })).StatusCode);
    }

    [Fact]
    public async Task CustomersAndPaymentsOfAnotherPharmacy_AreNotFound()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var customerId = await CustomerWithCreditAsync(owner, limit: 30_000);

        var otherPharmacy = await api.CreatePharmacyAsync();
        var otherOwner = await api.LoggedInClientAsync(otherPharmacy, Roles.Owner);
        await OpenShiftAsync(otherOwner);

        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync($"/api/customers/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync($"/api/customers/{customerId}/credit")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.PutAsJsonAsync($"/api/customers/{customerId}/credit/limit", new { limit = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherOwner.PostAsJsonAsync("/api/credit-payments", new { customerId, method = "Cash", amount = 1_000 })).StatusCode);
        Assert.Empty((await JsonAsync(await otherOwner.GetAsync("/api/customers?search=41234567"))).EnumerateArray());
    }
}
