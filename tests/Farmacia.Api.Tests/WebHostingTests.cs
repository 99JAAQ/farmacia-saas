using System.Net;
using Farmacia.Api.Hosting;
using Farmacia.Application.Authorization;
using Farmacia.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Farmacia.Api.Tests;

/// <summary>The app and the API on one origin, the security headers, and what a fresh production database gets.</summary>
[Collection(ApiCollection.Name)]
public class WebHostingTests(ApiFactory api)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/caja")]
    [InlineData("/catalogo/01a165a8-8746-7ab7-8988-c7647a971ed2")] // a reload on a product's page
    public async Task TheAppsRoutes_GetTheApp_WithoutLoggingIn(string path)
    {
        var response = await api.CreateHttpsClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Farmacia de prueba", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheAppsFiles_AreServedAsWhatTheyAre_NotAsTheFallbackPage()
    {
        // With routing first, the fallback answered /assets/*.js with index.html and the browser refused to run it.
        var response = await api.CreateHttpsClient().GetAsync("/assets/app-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("farmacia", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/no-existe")]
    [InlineData("/api")]
    [InlineData("/hubs/otro")]
    public async Task AnUnknownApiRoute_IsNotFound_NeverTheApp(string path)
    {
        // A typo in a fetch must fail loudly, not "succeed" with an HTML page.
        var owner = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);

        var response = await owner.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EveryResponse_CarriesTheSecurityHeaders()
    {
        var response = await api.CreateHttpsClient().GetAsync("/caja");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Fact]
    public async Task AFreshProductionDatabase_GetsItsRolesAndOneAdministrator_ThatMustChangeThePassword()
    {
        var email = $"admin-{Guid.CreateVersion7():N}@farmacia.test";
        IConfiguration Settings(string password) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:AdminEmail"] = email,
                ["Bootstrap:AdminPassword"] = password,
            })
            .Build();

        await ProductionBootstrap.RunAsync(api.Services, Settings("Inicial2026"), NullLogger.Instance);
        // A restart with another password changes nothing: the bootstrap never resets an existing account.
        await ProductionBootstrap.RunAsync(api.Services, Settings("Otra2026x"), NullLogger.Instance);

        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        Assert.NotNull(admin);
        Assert.Null(admin.PharmacyId);
        Assert.True(admin.MustChangePassword);
        Assert.True(await users.IsInRoleAsync(admin, Roles.PlatformAdmin));
        Assert.True(await users.CheckPasswordAsync(admin, "Inicial2026"));
    }
}
