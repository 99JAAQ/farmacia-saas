namespace Farmacia.Application.Abstractions;

/// <summary>
/// The pharmacy (tenant) the current request acts on. It never comes from the request body:
/// the API reads it from the pharmacy_id claim of the signed JWT.
/// </summary>
public interface ICurrentPharmacy
{
    /// <summary>The pharmacy of the request; throws (401) when there is none.</summary>
    Guid PharmacyId { get; }

    /// <summary>
    /// The pharmacy, or null when the request has none (an anonymous endpoint such as the login).
    /// The database connection uses it to set the Row-Level Security context without failing.
    /// </summary>
    Guid? PharmacyIdOrNull { get; }
}
