namespace Farmacia.Domain.Customers;

/// <summary>
/// Colombian identification document types. Values match the DIAN codes used in electronic invoicing
/// (verify against the current DIAN technical annex before implementing invoicing).
/// </summary>
public enum DocumentType
{
    IdentityCard = 12,          // Tarjeta de identidad (TI)
    CitizenshipCard = 13,       // Cédula de ciudadanía (CC)
    ForeignerId = 22,           // Cédula de extranjería (CE)
    Nit = 31,                   // NIT
    Passport = 41,              // Pasaporte
    SpecialStayPermit = 47,     // Permiso especial de permanencia (PEP)
    TemporaryProtectionPermit = 48, // Permiso por protección temporal (PPT)
}
