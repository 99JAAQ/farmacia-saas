using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthEndpointTests(ApiFactory api)
{
    [Fact]
    public async Task Login_ReturnsAccessTokenInBody_AndRefreshTokenOnlyInASecureHttpOnlyCookie()
    {
        var email = await api.CreateUserAsync(api.PharmacyA, Roles.Cashier);
        var client = api.CreateHttpsClient(handleCookies: false);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.False(body.TryGetProperty("refreshToken", out _));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("farmacia_refresh=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401ProblemDetails()
    {
        var email = await api.CreateUserAsync(api.PharmacyA, Roles.Cashier);

        var response = await api.CreateHttpsClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Incorrecta123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Correo o contraseña incorrectos.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_ReturnsThePharmacyAndRoleOfTheToken()
    {
        var client = await api.LoggedInClientAsync(api.PharmacyB, Roles.StockKeeper);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.Equal(api.PharmacyB.ToString(), me.GetProperty("pharmacyId").GetString());
        Assert.Equal(Roles.StockKeeper, Assert.Single(me.GetProperty("roles").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Refresh_WithTheCookie_GivesANewWorkingAccessToken_AndLogoutEndsIt()
    {
        var email = await api.CreateUserAsync(api.PharmacyA, Roles.Owner);
        var client = api.CreateHttpsClient(); // keeps cookies, like a browser
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.Password })).EnsureSuccessStatusCode();

        var refreshed = await client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var accessToken = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var response = await api.CreateHttpsClient().PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
