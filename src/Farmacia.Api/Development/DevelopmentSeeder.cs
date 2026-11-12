using Farmacia.Application.Authorization;
using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Suppliers;
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Api.Development;

internal static class DevelopmentSeeder
{
    private sealed record SeedUser(string Email, string FullName, string Role);

    /// <summary>
    /// Idempotent: makes sure the development pharmacy, the roles and one user per role exist.
    /// Requests then act on that pharmacy because its users' tokens carry its Id.
    /// </summary>
    public static async Task SeedAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // The seeder works above the tenants (it creates the pharmacy itself), so Row-Level Security must let it
        // see every pharmacy. Only for this scope.
        services.GetRequiredService<TenantDatabaseScope>().BypassIsolation();

        var db = services.GetRequiredService<FarmaciaDbContext>();
        var pharmacy = await EnsurePharmacyAsync(app.Configuration, db);
        await EnsureRolesAsync(services.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        await EnsureUsersAsync(app.Configuration, services.GetRequiredService<UserManager<ApplicationUser>>(), pharmacy.Id);
        await EnsureSupplierAsync(app.Configuration, db, pharmacy.Id);
    }

    /// <summary>A supplier to type purchases against (there is no supplier screen yet).</summary>
    private static async Task EnsureSupplierAsync(IConfiguration configuration, FarmaciaDbContext db, Guid pharmacyId)
    {
        var name = configuration["Development:SupplierName"] ?? "Distribuidora de Desarrollo";
        var nit = Nit.Parse(configuration["Development:SupplierNit"]);

        if (await db.Suppliers.AnyAsync(s => s.PharmacyId == pharmacyId && s.TaxId == nit))
            return;

        db.Suppliers.Add(Supplier.Create(pharmacyId, name, nit.Value));
        await db.SaveChangesAsync();
    }

    private static async Task<Pharmacy> EnsurePharmacyAsync(IConfiguration configuration, FarmaciaDbContext db)
    {
        var name = configuration["Development:PharmacyName"] ?? "Droguería de Desarrollo";
        var nit = Nit.Parse(configuration["Development:PharmacyNit"]);

        var pharmacy = await db.Pharmacies.SingleOrDefaultAsync(p => p.TaxId == nit);
        if (pharmacy is null)
        {
            pharmacy = Pharmacy.Create(name, nit.Value);
            db.Pharmacies.Add(pharmacy);
            await db.SaveChangesAsync();
        }

        return pharmacy;
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }));
        }
    }

    private static async Task EnsureUsersAsync(IConfiguration configuration, UserManager<ApplicationUser> userManager, Guid pharmacyId)
    {
        var password = configuration["Development:UserPassword"]
            ?? throw new InvalidOperationException("Development:UserPassword is missing.");
        var users = configuration.GetSection("Development:Users").Get<SeedUser[]>() ?? [];

        foreach (var seed in users)
        {
            if (await userManager.FindByEmailAsync(seed.Email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                FullName = seed.FullName,
                // The platform administrator belongs to no pharmacy.
                PharmacyId = seed.Role == Roles.PlatformAdmin ? null : pharmacyId,
            };

            ThrowIfFailed(await userManager.CreateAsync(user, password));
            ThrowIfFailed(await userManager.AddToRoleAsync(user, seed.Role));
        }
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Development seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
