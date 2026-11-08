using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

/// <summary>Accounts payable over HTTP: what is owed, per invoice, paid by the owner. A pharmacy of its own per test.</summary>
[Collection(ApiCollection.Name)]
public class SupplierPaymentsEndpointTests(ApiFactory api)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>A confirmed invoice of 90.000 (10 boxes at 9.000). Credit invoices fell due on 1 Oct 2026.</summary>
    private static async Task<string> ConfirmedInvoiceAsync(HttpClient client, Guid supplierId, Guid productId, Guid boxId, string term = "Credit")
    {
        var created = await client.PostAsJsonAsync("/api/purchases", new
        {
            supplierId,
            invoiceNumber = $"FE-{Guid.NewGuid():N}"[..12],
            invoiceDate = "2026-09-01",
            paymentTerm = term,
            dueDate = term == "Credit" ? "2026-10-01" : null,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/purchases/{id}/items", new
        {
            productId, saleUnitId = boxId, quantity = 10, bonusQuantity = 0, unitCost = 9_000, lineDiscount = 0,
            vatRate = "Exempt", lotNumber = "L1", expirationDate = "2028-01-31",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/purchases/{id}/confirm", new { printedInvoiceTotal = 90_000 })).StatusCode);
        return id;
    }

    private static async Task<JsonElement[]> PayablesAsync(HttpClient client) =>
        (await JsonAsync(await client.GetAsync("/api/purchases/payables"))).EnumerateArray().ToArray();

    [Fact]
    public async Task ACreditInvoice_IsOwed_ThenPaidInParts_UntilItLeavesThePayables()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var keeper = await api.LoggedInClientAsync(pharmacyId, Roles.StockKeeper);
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var (supplierId, productId, boxId) = await api.SeedCatalogAsync(pharmacyId);
        var invoiceId = await ConfirmedInvoiceAsync(keeper, supplierId, productId, boxId);
        await ConfirmedInvoiceAsync(keeper, supplierId, productId, boxId, term: "Cash"); // paid on receipt: never owed

        // 1. The stock keeper sees what is owed: one invoice, overdue (it fell due on 1 Oct)
        var owed = Assert.Single(await PayablesAsync(keeper));
        Assert.Equal((invoiceId, 90_000m, true), (owed.GetProperty("purchaseId").GetString(), owed.GetProperty("balance").GetDecimal(), owed.GetProperty("isOverdue").GetBoolean()));
        Assert.True(owed.GetProperty("daysOverdue").GetInt32() > 0);

        // 2. ...but cannot pay
        Assert.Equal(HttpStatusCode.Forbidden, (await keeper.PostAsJsonAsync($"/api/purchases/{invoiceId}/payments",
            new { amount = 10_000, method = "BankTransfer", reference = "TRX-1" })).StatusCode);

        // 3. The owner pays 40.000 by transfer; the invoice shows the payment, who made it and the new balance
        var paid = await owner.PostAsJsonAsync($"/api/purchases/{invoiceId}/payments",
            new { amount = 40_000, method = "BankTransfer", reference = "TRX-981" });
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        var invoice = await JsonAsync(await owner.GetAsync(paid.Headers.Location));
        Assert.Equal((40_000m, 50_000m), (invoice.GetProperty("amountPaid").GetDecimal(), invoice.GetProperty("balance").GetDecimal()));
        var payment = Assert.Single(invoice.GetProperty("payments").EnumerateArray());
        Assert.Equal(("TRX-981", "Usuario Owner", "BankTransfer"),
            (payment.GetProperty("reference").GetString(), payment.GetProperty("paidByName").GetString(), payment.GetProperty("method").GetString()));

        // 4. Over the balance: refused, saying what is left
        var tooMuch = await owner.PostAsJsonAsync($"/api/purchases/{invoiceId}/payments", new { amount = 50_001, method = "Cash" });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("50.000", (await JsonAsync(tooMuch)).GetProperty("detail").GetString());

        // 5. The rest from the safe: nothing owed any more
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/purchases/{invoiceId}/payments", new { amount = 50_000, method = "Cash" })).StatusCode);
        Assert.Empty(await PayablesAsync(owner));
    }

    [Fact]
    public async Task ATransferWithoutItsNumber_OrACashPurchase_CannotBePaid()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var (supplierId, productId, boxId) = await api.SeedCatalogAsync(pharmacyId);
        var credit = await ConfirmedInvoiceAsync(owner, supplierId, productId, boxId);
        var cash = await ConfirmedInvoiceAsync(owner, supplierId, productId, boxId, term: "Cash");

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/purchases/{credit}/payments", new { amount = 1_000, method = "BankTransfer" })).StatusCode);
        var paidOnReceipt = await owner.PostAsJsonAsync($"/api/purchases/{cash}/payments", new { amount = 1_000, method = "Cash" });
        Assert.Equal(HttpStatusCode.BadRequest, paidOnReceipt.StatusCode);
        Assert.Contains("contado", (await JsonAsync(paidOnReceipt)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TheCashier_SeesNoPayables_AndAnotherPharmacysInvoice_IsNotFound()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var (supplierId, productId, boxId) = await api.SeedCatalogAsync(pharmacyId);
        var invoiceId = await ConfirmedInvoiceAsync(owner, supplierId, productId, boxId);

        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/purchases/payables")).StatusCode);

        var otherOwner = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.PostAsJsonAsync($"/api/purchases/{invoiceId}/payments", new { amount = 1_000, method = "Cash" })).StatusCode);
        Assert.Empty(await PayablesAsync(otherOwner));
    }
}
