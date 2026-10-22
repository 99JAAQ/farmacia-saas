using Farmacia.Domain.Common;

namespace Farmacia.Domain.Suppliers;

/// <summary>Laboratory or distributor the pharmacy buys from.</summary>
public class Supplier : TenantEntity
{
    private Supplier() { } // EF Core

    private Supplier(Guid pharmacyId) : base(pharmacyId) { }

    public string Name { get; private set; } = null!;
    public string TaxId { get; private set; } = null!;
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    public static Supplier Create(
        Guid pharmacyId, string name, string taxId,
        string? contactName = null, string? phone = null, string? email = null)
    {
        var supplier = new Supplier(pharmacyId);
        supplier.Update(name, taxId, contactName, phone, email);
        return supplier;
    }

    public void Update(string name, string taxId, string? contactName, string? phone, string? email)
    {
        Name = Guard.Required(name, "El nombre del proveedor");
        TaxId = Guard.Required(taxId, "El NIT del proveedor");
        ContactName = Guard.Optional(contactName);
        Phone = Guard.Optional(phone);
        Email = Guard.OptionalEmail(email);
    }
}
