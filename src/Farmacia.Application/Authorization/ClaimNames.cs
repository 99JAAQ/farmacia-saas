namespace Farmacia.Application.Authorization;

/// <summary>Custom claim names carried in the JWT (standard ones like "sub" come from JwtRegisteredClaimNames).</summary>
public static class ClaimNames
{
    /// <summary>The tenant. The API reads the current pharmacy from here, never from the request body.</summary>
    public const string PharmacyId = "pharmacy_id";

    public const string Role = "role";

    /// <summary>
    /// Present (value "true") while the user has a temporary password. Every business policy refuses a token that has
    /// it: the user can only change the password (or sign out) until then.
    /// </summary>
    public const string MustChangePassword = "pwd_change";
}
