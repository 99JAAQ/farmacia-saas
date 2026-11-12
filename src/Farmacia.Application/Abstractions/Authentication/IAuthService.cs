namespace Farmacia.Application.Abstractions.Authentication;

/// <summary>
/// Login and session management. Not a MediatR use case on purpose: it orchestrates Identity,
/// it has no business rules of its own.
/// </summary>
public interface IAuthService
{
    /// <exception cref="Common.Exceptions.UnauthorizedException">Wrong credentials or locked account.</exception>
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Exchanges a refresh token for a new pair. The old refresh token stops working (rotation).</summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">Unknown, expired or reused token.</exception>
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends the session the refresh token belongs to. Unknown tokens are ignored.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// The user changes their own password (also a temporary one). Every other session of the user ends, and a new one
    /// starts here, without the "must change password" mark.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">Wrong current password, or a weak new one.</exception>
    Task<AuthResult> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

/// <param name="PharmacyId">Null only for the platform administrator, who belongs to no pharmacy.</param>
/// <param name="MustChangePassword">The user has a temporary password: the app asks for a new one before anything else.</param>
public sealed record AuthenticatedUser(
    Guid Id, string Email, string FullName, Guid? PharmacyId, IReadOnlyList<string> Roles, bool MustChangePassword);
