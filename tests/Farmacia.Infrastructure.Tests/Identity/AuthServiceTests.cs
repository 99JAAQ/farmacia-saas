using System.Security.Cryptography;
using System.Text;
using Farmacia.Application.Abstractions.Authentication;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Farmacia.Infrastructure.Tests.Identity;

[Collection(SqlServerCollection.Name)]
public sealed class AuthServiceTests : IAsyncLifetime
{
    private const string Password = "Farmacia2026";

    private readonly SqlServerFixture _fixture;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _services;

    public AuthServiceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Farmacia"] = fixture.ConnectionString,
                ["Jwt:Issuer"] = "farmacia-api",
                ["Jwt:Audience"] = "farmacia-web",
                ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            })
            .Build();

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddInfrastructure(configuration);
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(Roles.Cashier))
            await roles.CreateAsync(new IdentityRole<Guid>(Roles.Cashier) { Id = Guid.CreateVersion7() });
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    // One DI scope per call, like separate HTTP requests: no state shared through the DbContext.
    private async Task<T> AuthAsync<T>(Func<IAuthService, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAuthService>());
    }

    private async Task<string> CreateCashierAsync()
    {
        var email = $"cajero-{Guid.CreateVersion7():N}@farmacia.test";

        await using var scope = _services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Cajero de Prueba", PharmacyId = _fixture.PharmacyId };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, Roles.Cashier)).Succeeded);
        return email;
    }

    private async Task<RefreshToken> StoredTokenAsync(string rawToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        await using var db = _fixture.CreateContext();
        return await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash);
    }

    [Fact]
    public async Task Login_ReturnsAccessToken_WithTenantRoleAndFifteenMinutes()
    {
        var email = await CreateCashierAsync();

        var result = await AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None));

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.AccessToken);
        Assert.Equal(result.User.Id.ToString(), jwt.Subject);
        Assert.Equal(_fixture.PharmacyId.ToString(), jwt.GetClaim(ClaimNames.PharmacyId).Value);
        Assert.Equal(Roles.Cashier, jwt.GetClaim(ClaimNames.Role).Value);
        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.IssuedAt);
        Assert.Equal([Roles.Cashier], result.User.Roles);
    }

    [Fact]
    public async Task Login_StoresOnlyTheHashOfTheRefreshToken()
    {
        var email = await CreateCashierAsync();

        var result = await AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None));

        await using var db = _fixture.CreateContext();
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.TokenHash == result.RefreshToken));
        var stored = await StoredTokenAsync(result.RefreshToken);
        Assert.Equal(_time.GetUtcNow().AddDays(7), stored.ExpiresAt);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_GiveTheSameMessage()
    {
        var email = await CreateCashierAsync();

        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.LoginAsync("nadie@farmacia.test", Password, CancellationToken.None)));
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.LoginAsync(email, "Incorrecta123", CancellationToken.None)));

        Assert.Equal(unknown.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        var email = await CreateCashierAsync();

        for (var attempt = 1; attempt <= 5; attempt++)
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                AuthAsync(auth => auth.LoginAsync(email, "Incorrecta123", CancellationToken.None)));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None)));
        Assert.Contains("bloqueada", ex.Message);
    }

    [Fact]
    public async Task Refresh_RotatesTheToken_AndTheOldOneIsMarkedAsReplaced()
    {
        var email = await CreateCashierAsync();
        var login = await AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None));

        var refreshed = await AuthAsync(auth => auth.RefreshAsync(login.RefreshToken, CancellationToken.None));

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        var oldToken = await StoredTokenAsync(login.RefreshToken);
        var newToken = await StoredTokenAsync(refreshed.RefreshToken);
        Assert.True(oldToken.IsRevoked);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);
        Assert.Equal(oldToken.FamilyId, newToken.FamilyId);
        Assert.False(newToken.IsRevoked);
    }

    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesTheWholeFamily()
    {
        var email = await CreateCashierAsync();
        var login = await AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None));

        // The attacker copied the first token and uses it first; the user then refreshes with the same copy.
        var attacker = await AuthAsync(auth => auth.RefreshAsync(login.RefreshToken, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.RefreshAsync(login.RefreshToken, CancellationToken.None)));

        // The reuse ended the session: the attacker's newer token no longer works either.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.RefreshAsync(attacker.RefreshToken, CancellationToken.None)));
        Assert.Equal("Reuse detected", (await StoredTokenAsync(attacker.RefreshToken)).RevokedReason);
    }

    [Fact]
    public async Task Refresh_AfterSevenDays_Fails()
    {
        var email = await CreateCashierAsync();
        var login = await AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None));

        _time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.RefreshAsync(login.RefreshToken, CancellationToken.None)));
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var email = await CreateCashierAsync();
        var login = await AuthAsync(auth => auth.LoginAsync(email, Password, CancellationToken.None));

        await AuthAsync(async auth => { await auth.LogoutAsync(login.RefreshToken, CancellationToken.None); return 0; });

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.RefreshAsync(login.RefreshToken, CancellationToken.None)));
        Assert.Equal("Logout", (await StoredTokenAsync(login.RefreshToken)).RevokedReason);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_Fails()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            AuthAsync(auth => auth.RefreshAsync("token-inventado", CancellationToken.None)));
    }
}
