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

    /// <summary>
    /// Type in and confirm supplier invoices, and see them: they show costs, so margins stay out of the cashier's view.
    /// To let only the owner confirm (separation of duties), split this policy.
    /// </summary>
    public const string ReceivePurchases = nameof(ReceivePurchases);

    /// <summary>Open, operate and close the cash register's shifts, and sell. Who may touch which shift is a use-case rule.</summary>
    public const string OperateCash = nameof(OperateCash);

    /// <summary>
    /// Give credit ("fiado"): register customers, open their account, set the limit, suspend it, see who owes.
    /// Cashiers sell on credit and receive payments (OperateCash), but never decide who gets credit or how much.
    /// </summary>
    public const string ManageCredit = nameof(ManageCredit);

    /// <summary>
    /// Write off expired batches, adjust batches to a physical count, and read the kardex. Every movement records who
    /// did it, so the owner can review the stock keeper's write-offs and adjustments.
    /// </summary>
    public const string ManageInventory = nameof(ManageInventory);

    /// <summary>
    /// Pay supplier invoices bought on credit. Money leaving the business: the owner only. The stock keeper still
    /// receives purchases and sees what is owed (ReceivePurchases).
    /// </summary>
    public const string PaySuppliers = nameof(PaySuppliers);

    /// <summary>Create pharmacies and their owners. The platform administrator only, who sees no pharmacy's data.</summary>
    public const string ManagePlatform = nameof(ManagePlatform);

    internal static void Configure(AuthorizationOptions options)
    {
        // Every endpoint requires a valid token of a pharmacy's user unless it says [AllowAnonymous] or names its own
        // policy: a controller that forgets [Authorize] stays protected instead of exposed. Requiring the pharmacy
        // claim keeps the platform administrator (who has none) out of every business endpoint with a 403.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimNames.PharmacyId)
            .Build();

        options.AddPolicy(ManageCatalog, policy => policy.RequireRole(Roles.Owner, Roles.StockKeeper));
        options.AddPolicy(ReceivePurchases, policy => policy.RequireRole(Roles.Owner, Roles.StockKeeper));
        options.AddPolicy(OperateCash, policy => policy.RequireRole(Roles.Owner, Roles.Cashier));
        options.AddPolicy(ManageCredit, policy => policy.RequireRole(Roles.Owner));
        options.AddPolicy(ManageInventory, policy => policy.RequireRole(Roles.Owner, Roles.StockKeeper));
        options.AddPolicy(PaySuppliers, policy => policy.RequireRole(Roles.Owner));
        options.AddPolicy(ManagePlatform, policy => policy.RequireRole(Roles.PlatformAdmin));
    }
}
