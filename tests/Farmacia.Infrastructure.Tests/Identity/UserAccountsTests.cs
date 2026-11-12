using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Authentication;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Identity;

/// <summary>The owner's actions on users, and their effect on sessions, against SQL Server.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class UserAccountsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Temporary = "Temporal2026";
    private const string Personal = "MiClave2026";

    private readonly ServiceProvider _services = InfrastructureServices.Build(fixture);

    public async Task InitializeAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
        }
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task ANewUser_StartsWithATemporaryPassword_AndChangingItEndsTheOtherSessions()
    {
        var (_, email) = await CreateCashierAsync();
        var first = await Auth(a => a.LoginAsync(email, Temporary, default));
        var second = await Auth(a => a.LoginAsync(email, Temporary, default));
        Assert.True(first.User.MustChangePassword);

        var changed = await Auth(a => a.ChangePasswordAsync(first.User.Id, Temporary, Personal, default));

        Assert.False(changed.User.MustChangePassword);
        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.RefreshAsync(second.RefreshToken, default)));
        Assert.False((await Auth(a => a.RefreshAsync(changed.RefreshToken, default))).User.MustChangePassword);
        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.LoginAsync(email, Temporary, default)));
    }

    [Fact]
    public async Task ChangingThePassword_NeedsTheCurrentOne()
    {
        var (_, email) = await CreateCashierAsync();
        var session = await Auth(a => a.LoginAsync(email, Temporary, default));

        var error = await Assert.ThrowsAsync<ValidationException>(() => Auth(a => a.ChangePasswordAsync(session.User.Id, "Otra2026x", Personal, default)));

        Assert.Equal("La contraseña actual no es correcta.", error.Message);
    }

    [Fact]
    public async Task ADeactivatedUser_CannotRefreshNorLogIn_UntilReactivated()
    {
        var (id, email) = await CreateCashierAsync();
        var session = await Auth(a => a.LoginAsync(email, Temporary, default));

        await Users(u => u.SetActiveAsync(fixture.PharmacyId, id, false, default));

        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.RefreshAsync(session.RefreshToken, default)));
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.LoginAsync(email, Temporary, default)));
        Assert.Contains("desactivado", error.Message);

        await Users(u => u.SetActiveAsync(fixture.PharmacyId, id, true, default));
        await Auth(a => a.LoginAsync(email, Temporary, default));
    }

    [Fact]
    public async Task ARefresh_ChecksThatTheUserIsStillActive_EvenIfItsTokenWasNotRevoked()
    {
        // A login that finishes just after the deactivation revoked the sessions leaves a live token behind.
        // Simulated by deactivating without revoking: the refresh itself must refuse.
        var (id, email) = await CreateCashierAsync();
        var session = await Auth(a => a.LoginAsync(email, Temporary, default));
        await using (var scope = _services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Infrastructure.Identity.ApplicationUser>>();
            var user = await userManager.FindByIdAsync(id.ToString());
            user!.IsActive = false;
            await userManager.UpdateAsync(user);
        }

        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.RefreshAsync(session.RefreshToken, default)));
    }

    [Fact]
    public async Task ResettingThePassword_EndsTheSessions_AndAsksForANewOne()
    {
        var (id, email) = await CreateCashierAsync();
        var mine = await Auth(a => a.LoginAsync(email, Temporary, default));
        await Auth(a => a.ChangePasswordAsync(mine.User.Id, Temporary, Personal, default));
        var session = await Auth(a => a.LoginAsync(email, Personal, default));

        await Users(u => u.ResetPasswordAsync(fixture.PharmacyId, id, "Reinicio2026", default));

        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.RefreshAsync(session.RefreshToken, default)));
        await Assert.ThrowsAsync<UnauthorizedException>(() => Auth(a => a.LoginAsync(email, Personal, default)));
        Assert.True((await Auth(a => a.LoginAsync(email, "Reinicio2026", default))).User.MustChangePassword);
    }

    [Fact]
    public async Task AWeakTemporaryPassword_ChangesNothing()
    {
        var (id, email) = await CreateCashierAsync();

        await Assert.ThrowsAsync<ValidationException>(() => Users(u => u.ResetPasswordAsync(fixture.PharmacyId, id, "corta", default)));

        await Auth(a => a.LoginAsync(email, Temporary, default)); // the old password still works
    }

    [Fact]
    public async Task ThePharmacysUsers_AreOnlyItsOwn_WithTheirRole()
    {
        var (id, _) = await CreateCashierAsync();

        var users = await Users(u => u.ListAsync(fixture.PharmacyId, default));
        var cashier = Assert.Single(users, u => u.Id == id);
        Assert.Equal(Roles.Cashier, cashier.Role);
        Assert.Null(await Users(u => u.GetAsync(Guid.CreateVersion7(), id, default))); // another pharmacy does not see it

        await Users(u => u.SetRoleAsync(fixture.PharmacyId, id, Roles.StockKeeper, default));
        Assert.Equal(Roles.StockKeeper, (await Users(u => u.GetAsync(fixture.PharmacyId, id, default)))!.Role);
        await Assert.ThrowsAsync<NotFoundException>(() => Users(u => u.SetActiveAsync(Guid.CreateVersion7(), id, false, default)));
    }

    private async Task<(Guid Id, string Email)> CreateCashierAsync()
    {
        var email = $"cajero-{Guid.CreateVersion7():N}@farmacia.test";
        var id = await Users(u => u.CreateAsync(fixture.PharmacyId, new NewUserAccount("Cajero Nuevo", email, Roles.Cashier, Temporary), default));
        return (id, email);
    }

    // One DI scope per call, like separate HTTP requests.
    private async Task<T> Auth<T>(Func<IAuthService, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAuthService>());
    }

    private async Task<T> Users<T>(Func<IUserAccounts, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IUserAccounts>());
    }

    private async Task Users(Func<IUserAccounts, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IUserAccounts>());
    }
}
