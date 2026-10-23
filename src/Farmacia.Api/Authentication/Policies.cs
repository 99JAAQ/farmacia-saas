using Farmacia.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Farmacia.Api.Authentication;

/// <summary>
/// Named authorization policies: "who may do what" lives here, not scattered as role strings across controllers.
/// </summary>
public static class Policies
{
    /// <summary>Create and edit products, categories and suppliers.</summary>
    public const string ManageCatalog = nameof(ManageCatalog);

    internal static void Configure(AuthorizationOptions options)
    {
        // Every endpoint requires a valid token unless it says [AllowAnonymous]:
        // a controller that forgets [Authorize] stays protected instead of exposed.
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        options.AddPolicy(ManageCatalog, policy => policy.RequireRole(Roles.Owner, Roles.StockKeeper));
    }
}
