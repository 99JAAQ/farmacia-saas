using Farmacia.Domain.Pharmacies;

namespace Farmacia.Application.Pharmacies;

/// <summary>The current pharmacy: what goes on the header of tickets and vouchers, and its settings.</summary>
public sealed record PharmacyDto(Guid Id, string Name, string TaxId, string? Address, string? Phone, bool IsVatResponsible)
{
    public static PharmacyDto From(Pharmacy pharmacy) => new(
        pharmacy.Id, pharmacy.Name, pharmacy.TaxId.Value, pharmacy.Address, pharmacy.Phone, pharmacy.IsVatResponsible);
}
