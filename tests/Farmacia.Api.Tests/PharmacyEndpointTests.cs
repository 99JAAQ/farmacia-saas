using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class PharmacyEndpointTests(ApiFactory api)
{
    [Fact]
    public async Task TheOwnerUpdatesTheTicketHeader_AndTheCashierSeesIt_ButOnlyInItsPharmacy()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var otherId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var otherCashier = await api.LoggedInClientAsync(otherId, Roles.Cashier);
        var nit = (await cashier.GetFromJsonAsync<JsonElement>("/api/pharmacy")).GetProperty("taxId").GetString();

        var response = await owner.PutAsJsonAsync("/api/pharmacy",
            new { name = "  Droguería La Esperanza ", address = "Calle 10 # 5-20", phone = "604 555 1234", isVatResponsible = false });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var seen = await cashier.GetFromJsonAsync<JsonElement>("/api/pharmacy");
        Assert.Equal("Droguería La Esperanza", seen.GetProperty("name").GetString());
        Assert.Equal("Calle 10 # 5-20", seen.GetProperty("address").GetString());
        Assert.Equal(nit, seen.GetProperty("taxId").GetString()); // the NIT does not change
        Assert.False(seen.GetProperty("isVatResponsible").GetBoolean());

        var other = await otherCashier.GetFromJsonAsync<JsonElement>("/api/pharmacy");
        Assert.Equal(otherId, other.GetProperty("id").GetGuid());
        Assert.Equal("Droguería de Caja", other.GetProperty("name").GetString());
    }

    [Fact]
    public async Task OnlyTheOwner_EditsThePharmacy()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var body = new { name = "Cambiada", address = (string?)null, phone = (string?)null, isVatResponsible = true };

        foreach (var role in new[] { Roles.Cashier, Roles.StockKeeper })
        {
            var client = await api.LoggedInClientAsync(pharmacyId, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/pharmacy", body)).StatusCode);
        }

        var admin = await api.LoggedInPlatformAdminAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/pharmacy")).StatusCode);
    }

    [Fact]
    public async Task AnEmptyName_IsRejected()
    {
        var owner = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);

        var response = await owner.PutAsJsonAsync("/api/pharmacy", new { name = " ", address = (string?)null, phone = (string?)null, isVatResponsible = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
