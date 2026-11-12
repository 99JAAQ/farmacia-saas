using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;

namespace Farmacia.Application.Users;

/// <summary>The rules shared by the use cases that change a user of the pharmacy.</summary>
internal static class UserRules
{
    public static void EnsurePharmacyRole(string? role)
    {
        if (role is null || !Roles.PharmacyRoles.Contains(role))
            throw new ValidationException("El rol debe ser dueño, cajero o bodeguero.");
    }

    /// <summary>A user of another pharmacy is "not found" (404), never "forbidden": it must not reveal it exists.</summary>
    public static async Task<UserAccountDto> GetAsync(
        IUserAccounts users, Guid pharmacyId, Guid userId, CancellationToken cancellationToken) =>
        await users.GetAsync(pharmacyId, userId, cancellationToken)
            ?? throw new NotFoundException("No se encontró el usuario.");

    /// <summary>
    /// The owner cannot lock themselves out (deactivate or demote themselves): another owner has to do it. Their own
    /// password changes through "Cambiar contraseña", which asks for the current one.
    /// </summary>
    public static void EnsureNotSelf(Guid userId, ICurrentUser currentUser, string message)
    {
        if (userId == currentUser.UserId)
            throw new ValidationException(message);
    }

    /// <summary>
    /// The pharmacy always keeps an active owner: nobody else can create users or edit it. Checked even though the
    /// owner cannot change themselves, because a just-deactivated owner keeps a valid access token for minutes.
    /// </summary>
    public static async Task EnsureAnotherActiveOwnerAsync(
        IUserAccounts users, Guid pharmacyId, UserAccountDto user, CancellationToken cancellationToken)
    {
        if (user is { Role: Roles.Owner, IsActive: true } && await users.CountActiveOwnersAsync(pharmacyId, cancellationToken) <= 1)
            throw new ValidationException("La farmacia debe tener al menos un dueño activo.");
    }
}
