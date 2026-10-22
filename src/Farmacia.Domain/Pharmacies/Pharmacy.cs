using Farmacia.Domain.Common;

namespace Farmacia.Domain.Pharmacies;

/// <summary>The tenant. Every business entity points to it through <see cref="TenantEntity.PharmacyId"/>.</summary>
public class Pharmacy : Entity
{
    private Pharmacy() { } // EF Core

    public string Name { get; private set; } = null!;
    public string TaxId { get; private set; } = null!;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }

    public static Pharmacy Create(string name, string taxId, string? address = null, string? phone = null)
    {
        var pharmacy = new Pharmacy();
        pharmacy.Update(name, taxId, address, phone);
        return pharmacy;
    }

    public void Update(string name, string taxId, string? address, string? phone)
    {
        Name = Guard.Required(name, "El nombre de la farmacia");
        TaxId = Guard.Required(taxId, "El NIT de la farmacia");
        Address = Guard.Optional(address);
        Phone = Guard.Optional(phone);
    }
}
