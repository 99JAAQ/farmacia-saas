using Farmacia.Domain.Common;

namespace Farmacia.Domain.Pharmacies;

/// <summary>The tenant. Every business entity points to it through <see cref="TenantEntity.PharmacyId"/>.</summary>
public class Pharmacy : Entity
{
    private Pharmacy() { } // EF Core

    public string Name { get; private set; } = null!;
    public Nit TaxId { get; private set; } = null!;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>
    /// "Responsable de IVA": the VAT paid on purchases is recovered from the VAT charged on sales, so it is not part
    /// of the inventory cost. When false, purchase VAT is a cost. To confirm with the pharmacy's accountant.
    /// </summary>
    public bool IsVatResponsible { get; private set; } = true;

    public static Pharmacy Create(string name, string taxId, string? address = null, string? phone = null)
    {
        var pharmacy = new Pharmacy();
        pharmacy.Update(name, taxId, address, phone);
        return pharmacy;
    }

    public void Update(string name, string taxId, string? address, string? phone)
    {
        Name = Guard.Required(name, "El nombre de la farmacia");
        TaxId = Nit.Parse(taxId);
        Address = Guard.Optional(address);
        Phone = Guard.Optional(phone);
    }

    /// <summary>
    /// What the owner may change: how the pharmacy is presented on tickets. The NIT stays: it is the legal identity
    /// the platform registered the pharmacy with (unique across the platform).
    /// </summary>
    public void UpdateDetails(string name, string? address, string? phone) => Update(name, TaxId.Value, address, phone);

    public void SetVatResponsibility(bool isVatResponsible) => IsVatResponsible = isVatResponsible;
}
