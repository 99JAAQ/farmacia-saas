namespace Farmacia.Application.Abstractions;

/// <summary>
/// The pharmacy (tenant) the current request acts on. It never comes from the request body:
/// the API reads it from the pharmacy_id claim of the signed JWT.
/// </summary>
public interface ICurrentPharmacy
{
    Guid PharmacyId { get; }
}
