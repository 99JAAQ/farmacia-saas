using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Farmacia.Application.Abstractions.Authentication;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Farmacia.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    FarmaciaDbContext db,
    AccessTokenIssuer accessTokens,
    IOptions<JwtOptions> options,
    TimeProvider time) : IAuthService
{
    // Same message for unknown email and wrong password: different messages would reveal which emails exist.
    private const string InvalidCredentials = "Correo o contraseña incorrectos.";
    private const string LockedOut = "La cuenta está bloqueada temporalmente por intentos fallidos. Intente más tarde.";
    private const string InvalidSession = "La sesión no es válida o expiró. Inicie sesión de nuevo.";

    // Verified when the email does not exist, so the response takes as long as a real password check
    // and timing does not reveal which emails are registered either.
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, password ?? "");
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException(LockedOut);

        if (!await userManager.CheckPasswordAsync(user, password ?? ""))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException(await userManager.IsLockedOutAsync(user) ? LockedOut : InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await IssueAsync(user, familyId: Guid.CreateVersion7(), replacing: null, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var stored = await FindAsync(refreshToken, cancellationToken)
            ?? throw new UnauthorizedException(InvalidSession);

        if (stored.IsRevoked)
        {
            // Each token works once. If a used one comes back, someone copied it, and whoever holds the newest
            // token of this family may be the attacker: end the whole session.
            await RevokeFamilyAsync(stored.FamilyId, now, "Reuse detected", cancellationToken);
            throw new UnauthorizedException(InvalidSession);
        }

        if (stored.IsExpired(now))
            throw new UnauthorizedException(InvalidSession);

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(stored.FamilyId, now, "User unavailable", cancellationToken);
            throw new UnauthorizedException(InvalidSession);
        }

        return await IssueAsync(user, stored.FamilyId, replacing: stored, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await FindAsync(refreshToken, cancellationToken);
        if (stored is not null)
            await RevokeFamilyAsync(stored.FamilyId, time.GetUtcNow(), "Logout", cancellationToken);
    }

    private async Task<AuthResult> IssueAsync(
        ApplicationUser user, Guid familyId, RefreshToken? replacing, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var (accessToken, accessTokenExpiresAt) = accessTokens.Issue(user, roles);

        // 64 random bytes: impossible to guess, so a plain SHA-256 is enough to store it (no salt needed, unlike passwords).
        var rawRefreshToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        var refreshToken = new RefreshToken(
            user.Id, familyId, Hash(rawRefreshToken), now, now.AddDays(options.Value.RefreshTokenDays));

        db.RefreshTokens.Add(refreshToken);
        replacing?.Revoke(now, "Rotated", refreshToken.Id);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(
            accessToken,
            accessTokenExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt,
            new AuthenticatedUser(user.Id, user.Email!, user.FullName, user.PharmacyId, roles));
    }

    private Task<RefreshToken?> FindAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return Task.FromResult<RefreshToken?>(null);

        var hash = Hash(rawToken);
        return db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason), cancellationToken);

    internal static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
