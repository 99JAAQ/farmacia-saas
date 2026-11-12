using Farmacia.Application.Abstractions.Authentication;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Farmacia.Api.Controllers;

/// <summary>
/// The access token travels in the response body (the client keeps it in memory only).
/// The refresh token travels only in an HttpOnly cookie: JavaScript can never read it, so an XSS cannot steal it.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    private const string RefreshCookieName = "farmacia_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        StartSession(await auth.LoginAsync(request.Email, request.Password, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedException("No hay una sesión activa. Inicie sesión.");

        return StartSession(await auth.RefreshAsync(refreshToken, cancellationToken));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await auth.LogoutAsync(refreshToken, cancellationToken);

        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(expires: null));
        return NoContent();
    }

    /// <summary>
    /// The signed-in user changes their password, also a temporary one (default policy: allowed while the token still
    /// says "must change password"). Every other session ends; this one continues with a new token and cookie.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> ChangePassword(
        ChangePasswordRequest request, [FromServices] Application.Abstractions.ICurrentUser currentUser, CancellationToken cancellationToken) =>
        StartSession(await auth.ChangePasswordAsync(currentUser.UserId, request.CurrentPassword, request.NewPassword, cancellationToken));

    /// <summary>Who the access token belongs to. Lets the client check that its token works.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me() => new CurrentUserResponse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        User.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
        User.Identity?.Name,
        User.FindFirst(ClaimNames.PharmacyId)?.Value,
        User.FindAll(ClaimNames.Role).Select(c => c.Value).ToList(),
        User.HasClaim(c => c.Type == ClaimNames.MustChangePassword));

    private SessionResponse StartSession(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, RefreshCookieOptions(result.RefreshTokenExpiresAt));
        return new SessionResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }

    private static CookieOptions RefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,                 // invisible to JavaScript
        Secure = true,                   // HTTPS only (browsers treat localhost as secure)
        SameSite = SameSiteMode.Strict,  // never sent by requests coming from other sites (CSRF)
        Path = "/api/auth",              // not sent to the rest of the API
        Expires = expires,
        IsEssential = true,
    };
}

public sealed record LoginRequest(string Email, string Password);

public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AuthenticatedUser User);

public sealed record CurrentUserResponse(
    string? UserId, string? Email, string? FullName, string? PharmacyId, IReadOnlyList<string> Roles, bool MustChangePassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
