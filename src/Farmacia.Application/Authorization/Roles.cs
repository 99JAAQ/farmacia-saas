namespace Farmacia.Application.Authorization;

/// <summary>Role names, as stored by Identity and carried in the JWT.</summary>
public static class Roles
{
    /// <summary>Dueño: full access to the pharmacy, manages users.</summary>
    public const string Owner = "Owner";

    /// <summary>Cajero: sells and manages the cash session.</summary>
    public const string Cashier = "Cashier";

    /// <summary>Bodeguero: receives purchases and manages inventory.</summary>
    public const string StockKeeper = "StockKeeper";

    /// <summary>
    /// Administrador de la plataforma: creates pharmacies and their owners. Belongs to no pharmacy, so it never sees
    /// a pharmacy's sales, stock or customers (its token has no pharmacy_id).
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>The roles a pharmacy's own users can have.</summary>
    public static readonly IReadOnlyList<string> PharmacyRoles = [Owner, Cashier, StockKeeper];

    public static readonly IReadOnlyList<string> All = [.. PharmacyRoles, PlatformAdmin];
}
