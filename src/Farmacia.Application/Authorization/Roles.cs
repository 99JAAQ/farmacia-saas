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

    public static readonly IReadOnlyList<string> All = [Owner, Cashier, StockKeeper];
}
