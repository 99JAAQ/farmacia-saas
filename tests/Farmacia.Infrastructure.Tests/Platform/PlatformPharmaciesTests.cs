using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Authentication;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Platform;

/// <summary>Creating a pharmacy with its owner, above the tenants (Row-Level Security bypassed in its own scope).</summary>
[Collection(SqlServerCollection.Name)]
public sealed class PlatformPharmaciesTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Password = "Farmacia2026";

    // Like a request of the platform administrator: no pharmacy in the session.
    private readonly ServiceProvider _services = InfrastructureServices.Build(fixture, noPharmacy: true);

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
    public async Task Create_SavesThePharmacyAndAnOwnerWhoCanLogInToIt()
    {
        var pharmacy = NewPharmacy();
        var email = UniqueEmail();

        await CreateAsync(pharmacy, new NewOwner("Ana Gómez", email, Password));

        var session = await WithScopeAsync(s => s.GetRequiredService<IAuthService>().LoginAsync(email, Password, CancellationToken.None));
        Assert.Equal(pharmacy.Id, session.User.PharmacyId);
        Assert.Equal([Roles.Owner], session.User.Roles);

        var listed = Assert.Single(await ListAsync(), p => p.Id == pharmacy.Id);
        Assert.Equal("Ana Gómez", Assert.Single(listed.Owners).FullName);
        Assert.Equal(1, listed.UserCount);
    }

    [Fact]
    public async Task Create_WithAWeakPassword_LeavesNoPharmacyBehind()
    {
        // The pharmacy is saved before Identity checks the password: only the transaction undoes it.
        var pharmacy = NewPharmacy();

        var error = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(pharmacy, new NewOwner("Ana", UniqueEmail(), "corta")));

        Assert.Contains("al menos 8 caracteres", error.Message);
        await using var db = fixture.CreateContext();
        Assert.False(await db.Pharmacies.AnyAsync(p => p.Id == pharmacy.Id));
    }

    [Fact]
    public async Task Create_WithATakenNitOrEmail_IsAConflict()
    {
        var first = NewPharmacy();
        var email = UniqueEmail();
        await CreateAsync(first, new NewOwner("Ana", email, Password));

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateAsync(Pharmacy.Create("Otra", first.TaxId.Value), new NewOwner("Luis", UniqueEmail(), Password)));
        var other = NewPharmacy();
        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(other, new NewOwner("Luis", email, Password)));

        await using var db = fixture.CreateContext();
        Assert.False(await db.Pharmacies.AnyAsync(p => p.Id == other.Id));
    }

    private async Task CreateAsync(Pharmacy pharmacy, NewOwner owner) =>
        await WithScopeAsync(async s =>
        {
            await s.GetRequiredService<IPlatformPharmacies>().CreateAsync(pharmacy, owner, CancellationToken.None);
            return true;
        });

    private Task<IReadOnlyList<Application.Platform.PlatformPharmacyDto>> ListAsync() =>
        WithScopeAsync(s => s.GetRequiredService<IPlatformPharmacies>().ListAsync(CancellationToken.None));

    // One DI scope per call, like separate HTTP requests.
    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static Pharmacy NewPharmacy() =>
        Pharmacy.Create("Droguería Nueva", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);

    private static string UniqueEmail() => $"dueno-{Guid.CreateVersion7():N}@farmacia.test";
}
