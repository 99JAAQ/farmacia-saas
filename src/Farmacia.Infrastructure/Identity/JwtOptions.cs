using Microsoft.IdentityModel.Tokens;

namespace Farmacia.Infrastructure.Identity;

/// <summary>
/// Bound from the "Jwt" section. SigningKey is a secret: user-secrets in development, Key Vault in production.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;

    /// <summary>Base64-encoded random key of at least <see cref="MinimumKeyBytes"/> bytes.</summary>
    public string SigningKey { get; set; } = null!;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    public SymmetricSecurityKey CreateSigningKey() => new(Convert.FromBase64String(SigningKey));

    internal bool IsValid() =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && AccessTokenMinutes > 0
        && RefreshTokenDays > 0
        && IsStrongKey(SigningKey);

    private static bool IsStrongKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        var buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out var length) || length < MinimumKeyBytes)
            return false;

        // Rejects degenerate keys such as all zeros, e.g. from a key-generation script that silently failed.
        var key = buffer.AsSpan(0, length);
        return key.IndexOfAnyExcept(key[0]) >= 0;
    }
}
