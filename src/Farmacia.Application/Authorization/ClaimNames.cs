namespace Farmacia.Application.Authorization;

/// <summary>Custom claim names carried in the JWT (standard ones like "sub" come from JwtRegisteredClaimNames).</summary>
public static class ClaimNames
{
    /// <summary>The tenant. The API reads the current pharmacy from here, never from the request body.</summary>
    public const string PharmacyId = "pharmacy_id";

    public const string Role = "role";
}
