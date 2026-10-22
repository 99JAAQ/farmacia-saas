using Farmacia.Domain.Common;

namespace Farmacia.Domain.Customers;

/// <summary>Registered customer. Optional on a sale: most counter sales are anonymous.</summary>
public class Customer : TenantEntity
{
    private Customer() { } // EF Core

    private Customer(Guid pharmacyId) : base(pharmacyId) { }

    public string FullName { get; private set; } = null!;
    public DocumentType DocumentType { get; private set; }
    public string DocumentNumber { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    public static Customer Create(
        Guid pharmacyId, string fullName, DocumentType documentType, string documentNumber,
        string? phone = null, string? email = null)
    {
        var customer = new Customer(pharmacyId);
        customer.Update(fullName, documentType, documentNumber, phone, email);
        return customer;
    }

    public void Update(string fullName, DocumentType documentType, string documentNumber, string? phone, string? email)
    {
        // An enum can hold any int (e.g. a value cast from a bad request), so check it explicitly.
        if (!Enum.IsDefined(documentType))
            throw new DomainException("El tipo de documento no es válido.");

        FullName = Guard.Required(fullName, "El nombre del cliente");
        DocumentType = documentType;
        DocumentNumber = documentType == DocumentType.Nit
            ? Nit.Parse(documentNumber).Value
            : Guard.Required(documentNumber, "El documento del cliente");
        Phone = Guard.Optional(phone);
        Email = Guard.OptionalEmail(email);
    }
}
