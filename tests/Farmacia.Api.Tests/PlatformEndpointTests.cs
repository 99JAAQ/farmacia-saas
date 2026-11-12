using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;
using Farmacia.Domain.Common;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class PlatformEndpointTests(ApiFactory api)
{
    private const string Url = "/api/platform/pharmacies";

    [Fact]
    public async Task TheAdministratorCreatesAPharmacy_AndItsOwnerStartsWorkingInIt()
    {
        var admin = await api.LoggedInPlatformAdminAsync();
        var email = UniqueEmail();

        var response = await admin.PostAsJsonAsync(Url, NewPharmacy(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var pharmacyId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // The owner logs in to the new pharmacy, which starts empty (and does not see the other pharmacies' products).
        var owner = await api.LoggedInAsAsync(email, ApiFactory.Password);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(pharmacyId.ToString(), me.GetProperty("pharmacyId").GetString());
        Assert.Equal(0, (await owner.GetFromJsonAsync<JsonElement>("/api/products/catalog")).GetArrayLength());

        var listed = (await admin.GetFromJsonAsync<JsonElement>(Url)).EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == pharmacyId);
        Assert.Equal(email, listed.GetProperty("owners")[0].GetProperty("email").GetString());
        Assert.False(listed.GetProperty("isVatResponsible").GetBoolean());
    }

    [Fact]
    public async Task TheAdministrator_SeesNoPharmacysBusinessData()
    {
        var admin = await api.LoggedInPlatformAdminAsync();

        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(JsonValueKind.Null, me.GetProperty("pharmacyId").ValueKind);

        // Endpoints open to any user of a pharmacy (fallback policy) and endpoints with a role policy alike.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/products?search=aceta")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/products/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/cash-sessions/current")).StatusCode);
    }

    [Fact]
    public async Task OnlyTheAdministrator_ManagesPharmacies()
    {
        var owner = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);
        var anonymous = api.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(Url, NewPharmacy(UniqueEmail()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task InvalidData_IsRejected_AndATakenNitIsAConflict()
    {
        var admin = await api.LoggedInPlatformAdminAsync();

        var wrongDigit = await admin.PostAsJsonAsync(Url, NewPharmacy(UniqueEmail()) with { TaxId = "900123456-1" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongDigit.StatusCode);

        var weakPassword = await admin.PostAsJsonAsync(Url, NewPharmacy(UniqueEmail()) with { OwnerPassword = "farmacia" });
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
        Assert.Contains("mayúscula", await weakPassword.Content.ReadAsStringAsync());

        var first = NewPharmacy(UniqueEmail());
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Url, first)).StatusCode);
        var sameNit = await admin.PostAsJsonAsync(Url, NewPharmacy(UniqueEmail()) with { TaxId = first.TaxId });
        Assert.Equal(HttpStatusCode.Conflict, sameNit.StatusCode);
    }

    private static NewPharmacyRequest NewPharmacy(string ownerEmail) => new(
        "Droguería Nueva",
        Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value,
        "Calle 10 # 5-20",
        "604 555 1234",
        IsVatResponsible: false,
        "Dueña Nueva",
        ownerEmail,
        ApiFactory.Password);

    private static string UniqueEmail() => $"dueno-{Guid.CreateVersion7():N}@farmacia.test";

    private sealed record NewPharmacyRequest(
        string Name,
        string TaxId,
        string? Address,
        string? Phone,
        bool IsVatResponsible,
        string OwnerFullName,
        string OwnerEmail,
        string OwnerPassword);
}
