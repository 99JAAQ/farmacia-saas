using Farmacia.Application.Authorization;
using Farmacia.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Farmacia.Api.Hosting;

/// <summary>
/// What a fresh production database needs before anyone can log in, done at startup and idempotent (safe on every
/// restart): the roles, and the first platform administrator. Development has its own seeder with sample data; this
/// creates no data of any pharmacy.
/// </summary>
public static class ProductionBootstrap
{
    public const string SectionName = "Bootstrap";

    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                ThrowIfFailed(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }), $"role {role}");
        }

        await EnsureAdministratorAsync(users, configuration.GetSection(SectionName), logger);
    }

    /// <summary>
    /// The platform administrator who creates the first pharmacy. Created once, from configuration (the password comes
    /// from Key Vault), with a temporary password that must be changed at the first login (ADR 0016): whoever deployed
    /// knows it. Never touched again: a later restart neither resets the password nor recreates a deleted user's role.
    /// </summary>
    private static async Task EnsureAdministratorAsync(UserManager<ApplicationUser> users, IConfiguration section, ILogger logger)
    {
        var email = section["AdminEmail"];
        var password = section["AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return; // nothing configured: the administrator already exists, or will be created by another deployment

        if (await users.FindByEmailAsync(email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = section["AdminName"] ?? "Administrador de la plataforma",
            PharmacyId = null,
            MustChangePassword = true,
        };
        ThrowIfFailed(await users.CreateAsync(admin, password), "the platform administrator");
        ThrowIfFailed(await users.AddToRoleAsync(admin, Roles.PlatformAdmin), "the platform administrator's role");
        logger.LogInformation("Created the platform administrator {Email}; the password must be changed at the first login.", email);
    }

    private static void ThrowIfFailed(IdentityResult result, string what)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Startup could not create {what}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
