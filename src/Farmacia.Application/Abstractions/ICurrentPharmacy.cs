namespace Farmacia.Application.Abstractions;

/// <summary>
/// The pharmacy (tenant) the current request acts on. It never comes from the request body:
/// today it is a fixed development pharmacy; with JWT it will come from the user's claims.
/// </summary>
public interface ICurrentPharmacy
{
    Guid PharmacyId { get; }
}
