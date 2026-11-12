using Farmacia.Application.Users;

namespace Farmacia.Application.Abstractions;

/// <summary>
/// A pharmacy's user accounts, over ASP.NET Core Identity (Infrastructure). Identity tables are not under Row-Level
/// Security (ADR 0014), so every method takes the pharmacy and filters by it: isolation by construction.
/// The rules (who may do what, never zero owners) live in the use cases, not here.
/// </summary>
public interface IUserAccounts
{
    Task<IReadOnlyList<UserAccountDto>> ListAsync(Guid pharmacyId, CancellationToken cancellationToken);

    /// <summary>Null when the user does not exist or belongs to another pharmacy.</summary>
    Task<UserAccountDto?> GetAsync(Guid pharmacyId, Guid userId, CancellationToken cancellationToken);

    Task<int> CountActiveOwnersAsync(Guid pharmacyId, CancellationToken cancellationToken);

    /// <summary>The new user must change the temporary password at the first login.</summary>
    /// <exception cref="Common.Exceptions.ConflictException">The email is already registered on the platform.</exception>
    /// <exception cref="Common.Exceptions.ValidationException">The email or the password is not valid.</exception>
    Task<Guid> CreateAsync(Guid pharmacyId, NewUserAccount user, CancellationToken cancellationToken);

    Task SetRoleAsync(Guid pharmacyId, Guid userId, string role, CancellationToken cancellationToken);

    /// <summary>Deactivating also ends the user's sessions: no refresh will work (the access token lasts up to 15 minutes).</summary>
    Task SetActiveAsync(Guid pharmacyId, Guid userId, bool isActive, CancellationToken cancellationToken);

    /// <summary>Sets a temporary password, ends the user's sessions, and makes the user change it at the next login.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">The password is too weak.</exception>
    Task ResetPasswordAsync(Guid pharmacyId, Guid userId, string temporaryPassword, CancellationToken cancellationToken);
}

public sealed record NewUserAccount(string FullName, string Email, string Role, string TemporaryPassword);
