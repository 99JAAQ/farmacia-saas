using Farmacia.Application.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Farmacia.Infrastructure.Identity;

/// <summary>Creates the short-lived JWT the client sends on every request.</summary>
internal sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider time)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(ApplicationUser user, IReadOnlyList<string> roles)
    {
        var jwt = options.Value;
        var now = time.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email!,
                [JwtRegisteredClaimNames.Name] = user.FullName,
                [ClaimNames.PharmacyId] = user.PharmacyId.ToString(),
                [ClaimNames.Role] = roles.ToArray(),
            },
        };

        return (_handler.CreateToken(descriptor), expiresAt);
    }
}
