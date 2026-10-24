namespace Farmacia.Infrastructure.Persistence;

/// <summary>Helpers for free-text searches typed at the counter.</summary>
internal static class SearchPattern
{
    /// <summary>
    /// Case- and accent-insensitive, applied only to the search: "acetaminofen" finds "Acetaminofén".
    /// The database default (SQL_Latin1_General_CP1_CI_AS) ignores case but not accents.
    /// </summary>
    public const string Collation = "Latin1_General_100_CI_AI";

    /// <summary>
    /// A LIKE pattern for "contains", with the user's text escaped so "50%" or "a_b" are taken literally.
    /// Note: '%text%' cannot use an index; fine for a pharmacy catalog, full-text search if it ever grows a lot.
    /// </summary>
    public static string Contains(string text) =>
        "%" + text.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
}
