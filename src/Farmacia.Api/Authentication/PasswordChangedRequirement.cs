using Farmacia.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Farmacia.Api.Authentication;

/// <summary>
/// The user no longer has a temporary password: the token has no pwd_change claim. Part of every named policy and of
/// the fallback policy, so the owner (or the platform administrator), who knows the temporary password, cannot use
/// the account; the API itself enforces it, not just the screen.
/// </summary>
public sealed class PasswordChangedRequirement : IAuthorizationRequirement;

internal sealed class PasswordChangedHandler : AuthorizationHandler<PasswordChangedRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PasswordChangedRequirement requirement)
    {
        if (!context.User.HasClaim(c => c.Type == ClaimNames.MustChangePassword))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
