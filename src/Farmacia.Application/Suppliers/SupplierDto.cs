using Farmacia.Domain.Suppliers;

namespace Farmacia.Application.Suppliers;

public sealed record SupplierDto(Guid Id, string Name, string TaxId, string? ContactName, string? Phone, string? Email)
{
    public static SupplierDto From(Supplier supplier) =>
        new(supplier.Id, supplier.Name, supplier.TaxId.Value, supplier.ContactName, supplier.Phone, supplier.Email);
}
