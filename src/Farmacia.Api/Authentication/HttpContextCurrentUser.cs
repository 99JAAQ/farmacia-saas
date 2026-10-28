using System.IdentityModel.Tokens.Jwt;
using Farmacia.Application.Abstractions;
using Farmacia.Application.Common.Exceptions;

namespace Farmacia.Api.Authentication;

/// <summary>The user comes from the signed JWT ("sub"), like the pharmacy: the client cannot impersonate anyone.</summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            ? userId
            : throw new UnauthorizedException("La sesión no indica el usuario. Inicie sesión de nuevo.");

    // RoleClaimType is "role" (JwtAuthenticationExtensions), so IsInRole reads the token's role claims.
    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) ?? false;
}
