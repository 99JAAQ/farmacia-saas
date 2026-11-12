using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Platform;
using Farmacia.Domain.Pharmacies;
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Platform;

/// <summary>
/// Works above the tenants, so it bypasses Row-Level Security, in a service scope of its own: the request's
/// connections keep the administrator's empty context, and the bypass cannot leak into anything else the request does.
/// Together with the development seeder, the only code that bypasses isolation.
/// </summary>
public sealed class PlatformPharmacies(IServiceScopeFactory scopes) : IPlatformPharmacies
{
    // SQL Server: unique index violation (2601) and unique constraint violation (2627).
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public async Task CreateAsync(Pharmacy pharmacy, NewOwner owner, CancellationToken cancellationToken)
    {
        await using var scope = CreateBypassScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Friendly messages for the usual cases; the unique indexes still guard against two requests at once.
        if (await db.Pharmacies.AnyAsync(p => p.TaxId == pharmacy.TaxId, cancellationToken))
            throw new ConflictException($"Ya existe una farmacia con el NIT {pharmacy.TaxId.Value}.");
        if (await users.FindByEmailAsync(owner.Email) is not null)
            throw new ConflictException($"Ya existe un usuario con el email {owner.Email}.");

        // UserManager saves through the same DbContext, so it joins this transaction: if the owner cannot be created
        // (e.g. a weak password), the pharmacy is rolled back too. No pharmacy is ever left without an owner.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Pharmacies.Add(pharmacy);
            await db.SaveChangesAsync(cancellationToken);

            var user = new ApplicationUser
            {
                UserName = owner.Email,
                Email = owner.Email,
                EmailConfirmed = true,
                FullName = owner.FullName,
                PharmacyId = pharmacy.Id,
            };
            ThrowIfFailed(await users.CreateAsync(user, owner.Password));
            ThrowIfFailed(await users.AddToRoleAsync(user, Roles.Owner));

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number))
        {
            throw new ConflictException("Ya existe una farmacia con ese NIT o un usuario con ese email.", ex);
        }
    }

    public async Task<IReadOnlyList<PlatformPharmacyDto>> ListAsync(CancellationToken cancellationToken)
    {
        await using var scope = CreateBypassScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();

        var pharmacies = await db.Pharmacies.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var ownerRoleIds = db.Roles.Where(r => r.Name == Roles.Owner).Select(r => r.Id);
        var users = await db.Users
            .Where(u => u.PharmacyId != null)
            .Select(u => new
            {
                PharmacyId = u.PharmacyId!.Value,
                u.FullName,
                Email = u.Email!,
                IsOwner = db.UserRoles.Any(ur => ur.UserId == u.Id && ownerRoleIds.Contains(ur.RoleId)),
            })
            .ToListAsync(cancellationToken);

        return pharmacies
            .Select(p =>
            {
                var staff = users.Where(u => u.PharmacyId == p.Id).ToList();
                var owners = staff.Where(u => u.IsOwner).OrderBy(u => u.FullName)
                    .Select(u => new PlatformOwnerDto(u.FullName, u.Email)).ToList();
                return new PlatformPharmacyDto(
                    p.Id, p.Name, p.TaxId.Value, p.Address, p.Phone, p.IsVatResponsible, owners, staff.Count);
            })
            .ToList();
    }

    private AsyncServiceScope CreateBypassScope()
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantDatabaseScope>().BypassIsolation();
        return scope;
    }

    /// <summary>A taken email is a conflict (409); a weak password or a malformed email, a validation error (400).</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
            return;

        var message = string.Join(" ", result.Errors.Select(e => e.Description));
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            throw new ConflictException(message);
        throw new ValidationException(message);
    }
}
