using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class UsersEndpointTests(ApiFactory api)
{
    private const string Temporary = "Temporal2026";

    [Fact]
    public async Task ANewCashier_MustChangeTheTemporaryPassword_BeforeWorking()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var email = UniqueEmail();

        var created = await owner.PostAsJsonAsync("/api/users", new { fullName = "Carlos Ruiz", email, role = Roles.Cashier, temporaryPassword = Temporary });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // With the temporary password the API itself refuses the work (the owner knows that password).
        var cashier = await api.LoggedInAsAsync(email, Temporary);
        Assert.True((await cashier.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/cash-sessions/current")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/products?search=aceta")).StatusCode);

        var changed = await cashier.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Temporary, newPassword = "MiClave2026" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var session = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());

        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/api/cash-sessions/current")).StatusCode);

        var listed = (await owner.GetFromJsonAsync<JsonElement>("/api/users")).EnumerateArray().Single(u => u.GetProperty("email").GetString() == email);
        Assert.Equal(Roles.Cashier, listed.GetProperty("role").GetString());
        Assert.False(listed.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task ADeactivatedUser_CannotRenewTheSession_NorLogIn()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var email = await api.CreateUserAsync(pharmacyId, Roles.Cashier);
        var cashier = await api.LoggedInAsAsync(email, ApiFactory.Password);
        var id = await IdOfAsync(owner, email);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/users/{id}/deactivate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await cashier.PostAsync("/api/auth/refresh", null)).StatusCode);
        var login = await api.CreateHttpsClient().PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/users/{id}/activate", null)).StatusCode);
        await api.LoggedInAsAsync(email, ApiFactory.Password);
    }

    [Fact]
    public async Task OnlyTheOwner_ManagesUsers()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        foreach (var role in new[] { Roles.Cashier, Roles.StockKeeper })
        {
            var client = await api.LoggedInClientAsync(pharmacyId, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        }

        var admin = await api.LoggedInPlatformAdminAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task AUserOfAnotherPharmacy_IsNotFound_AndNotListed()
    {
        var owner = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        var otherPharmacy = await api.CreatePharmacyAsync();
        var otherOwner = await api.LoggedInClientAsync(otherPharmacy, Roles.Owner);
        var strangerEmail = await api.CreateUserAsync(otherPharmacy, Roles.Cashier);
        var strangerId = await IdOfAsync(otherOwner, strangerEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/users/{strangerId}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync($"/api/users/{strangerId}/role", new { role = Roles.Owner })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync($"/api/users/{strangerId}/reset-password", new { temporaryPassword = Temporary })).StatusCode);
        Assert.DoesNotContain((await owner.GetFromJsonAsync<JsonElement>("/api/users")).EnumerateArray(), u => u.GetProperty("id").GetGuid() == strangerId);
    }

    [Fact]
    public async Task TheRules_TheOwnerCannotLockThemselvesOut_NorCreateAdministrators_NorRepeatAnEmail()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var ownerEmail = await api.CreateUserAsync(pharmacyId, Roles.Owner);
        var owner = await api.LoggedInAsAsync(ownerEmail, ApiFactory.Password);
        var ownerId = await IdOfAsync(owner, ownerEmail);

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/users/{ownerId}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/users/{ownerId}/role", new { role = Roles.Cashier })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsJsonAsync("/api/users", new { fullName = "X", email = UniqueEmail(), role = Roles.PlatformAdmin, temporaryPassword = Temporary })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync("/api/users", new { fullName = "X", email = ownerEmail, role = Roles.Cashier, temporaryPassword = Temporary })).StatusCode);
    }

    [Fact]
    public async Task AResetPassword_EndsTheSessions_AndMustBeChanged()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var email = await api.CreateUserAsync(pharmacyId, Roles.StockKeeper);
        var keeper = await api.LoggedInAsAsync(email, ApiFactory.Password);

        var reset = await owner.PostAsJsonAsync($"/api/users/{await IdOfAsync(owner, email)}/reset-password", new { temporaryPassword = Temporary });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await keeper.PostAsync("/api/auth/refresh", null)).StatusCode);
        var again = await api.LoggedInAsAsync(email, Temporary);
        Assert.Equal(HttpStatusCode.Forbidden, (await again.GetAsync("/api/products/catalog")).StatusCode);
    }

    private static async Task<Guid> IdOfAsync(HttpClient owner, string email) =>
        (await owner.GetFromJsonAsync<JsonElement>("/api/users")).EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();

    private static string UniqueEmail() => $"usuario-{Guid.CreateVersion7():N}@farmacia.test";
}
