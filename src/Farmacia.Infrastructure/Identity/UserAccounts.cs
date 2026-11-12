using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Users;
using Farmacia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Identity;

/// <summary>
/// A pharmacy's users over Identity. Identity tables are outside Row-Level Security (ADR 0014), so every query here
/// filters by the pharmacy it is given.
/// </summary>
internal sealed class UserAccounts(
    UserManager<ApplicationUser> userManager,
    FarmaciaDbContext db,
    TimeProvider time) : IUserAccounts
{
    public async Task<IReadOnlyList<UserAccountDto>> ListAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        (await Query(pharmacyId)
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.FullName)
            .ToListAsync(cancellationToken))
        .Select(u => u.ToDto())
        .ToList();

    public async Task<UserAccountDto?> GetAsync(Guid pharmacyId, Guid userId, CancellationToken cancellationToken) =>
        (await Query(pharmacyId).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken))?.ToDto();

    public Task<int> CountActiveOwnersAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        Query(pharmacyId).CountAsync(u => u.IsActive && u.Role == Roles.Owner, cancellationToken);

    public async Task<Guid> CreateAsync(Guid pharmacyId, NewUserAccount newUser, CancellationToken cancellationToken)
    {
        // The email is the login, unique across the whole platform: a cashier who worked at another pharmacy needs a
        // different one. The message does not say where it is in use.
        if (await userManager.FindByEmailAsync(newUser.Email) is not null)
            throw new ConflictException($"Ya existe un usuario con el email {newUser.Email}.");

        var user = new ApplicationUser
        {
            UserName = newUser.Email,
            Email = newUser.Email,
            EmailConfirmed = true,
            FullName = newUser.FullName,
            PharmacyId = pharmacyId,
            MustChangePassword = true, // the owner knows it
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ThrowIfFailed(await userManager.CreateAsync(user, newUser.TemporaryPassword));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, newUser.Role));
        await transaction.CommitAsync(cancellationToken);

        return user.Id;
    }

    public async Task SetRoleAsync(Guid pharmacyId, Guid userId, string role, CancellationToken cancellationToken)
    {
        var user = await FindAsync(pharmacyId, userId);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, await userManager.GetRolesAsync(user)));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, role));
        await transaction.CommitAsync(cancellationToken);
        // The new role reaches the user's token at the next refresh (15 minutes at most).
    }

    public async Task SetActiveAsync(Guid pharmacyId, Guid userId, bool isActive, CancellationToken cancellationToken)
    {
        var user = await FindAsync(pharmacyId, userId);
        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));

        if (!isActive)
            await db.RevokeAllAsync(user.Id, time.GetUtcNow(), "Deactivated", cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid pharmacyId, Guid userId, string temporaryPassword, CancellationToken cancellationToken)
    {
        var user = await FindAsync(pharmacyId, userId);

        // Validate first: a weak password must change nothing.
        foreach (var validator in userManager.PasswordValidators)
            ThrowIfFailed(await validator.ValidateAsync(userManager, user, temporaryPassword));

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, temporaryPassword);
        user.MustChangePassword = true;
        // Forgetting the password often comes after being locked out: start again with a clean count.
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user)); // saves the user too

        await db.RevokeAllAsync(user.Id, time.GetUtcNow(), "Password reset", cancellationToken);
    }

    /// <summary>
    /// The pharmacy's users with their single role (pharmacy users have exactly one). Projected to a class with
    /// property initializers, not to the DTO's constructor: EF can filter and sort on those (e.g. by Role) in SQL.
    /// </summary>
    private IQueryable<UserRow> Query(Guid pharmacyId) =>
        from user in db.Users
        where user.PharmacyId == pharmacyId
        select new UserRow
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Role = (from userRole in db.UserRoles
                    join role in db.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name).FirstOrDefault() ?? "",
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
        };

    private sealed class UserRow
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = null!;
        public string Email { get; init; } = null!;
        public string Role { get; init; } = null!;
        public bool IsActive { get; init; }
        public bool MustChangePassword { get; init; }

        public UserAccountDto ToDto() => new(Id, FullName, Email, Role, IsActive, MustChangePassword);
    }

    private async Task<ApplicationUser> FindAsync(Guid pharmacyId, Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && user.PharmacyId == pharmacyId
            ? user
            : throw new NotFoundException("No se encontró el usuario.");
    }

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
