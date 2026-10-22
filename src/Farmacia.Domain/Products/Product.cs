using Farmacia.Domain.Common;

namespace Farmacia.Domain.Products;

/// <summary>
/// Aggregate root. Stock is always measured in the base unit (e.g. tablet);
/// each <see cref="SaleUnit"/> converts to it with its own factor and price.
/// </summary>
public class Product : TenantEntity
{
    private readonly List<SaleUnit> _saleUnits = [];

    private Product() { } // EF Core

    private Product(Guid pharmacyId) : base(pharmacyId) { }

    public string Name { get; private set; } = null!;
    public string BaseUnitName { get; private set; } = null!;
    public string? ActiveIngredient { get; private set; }
    public string? Concentration { get; private set; }
    public string? PharmaceuticalForm { get; private set; }
    public string? SanitaryRegistration { get; private set; }
    public bool RequiresPrescription { get; private set; }
    public IReadOnlyCollection<SaleUnit> SaleUnits => _saleUnits.AsReadOnly();

    public static Product Create(
        Guid pharmacyId,
        string name,
        string baseUnitName,
        bool requiresPrescription,
        string? activeIngredient = null,
        string? concentration = null,
        string? pharmaceuticalForm = null,
        string? sanitaryRegistration = null)
    {
        return new Product(pharmacyId)
        {
            Name = Guard.Required(name, "El nombre del producto"),
            BaseUnitName = Guard.Required(baseUnitName, "La unidad base"),
            RequiresPrescription = requiresPrescription,
            ActiveIngredient = Guard.Optional(activeIngredient),
            Concentration = Guard.Optional(concentration),
            PharmaceuticalForm = Guard.Optional(pharmaceuticalForm),
            SanitaryRegistration = Guard.Optional(sanitaryRegistration),
        };
    }

    public SaleUnit AddSaleUnit(string name, int conversionFactor, decimal price, string? barcode = null)
    {
        var unit = new SaleUnit(PharmacyId, Id, name, conversionFactor, price, barcode);

        if (_saleUnits.Any(u => string.Equals(u.Name, unit.Name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"El producto ya tiene una unidad de venta llamada '{unit.Name}'.");

        if (unit.Barcode is not null && _saleUnits.Any(u => u.Barcode == unit.Barcode))
            throw new DomainException($"El código de barras '{unit.Barcode}' ya está asignado a otra unidad de venta.");

        _saleUnits.Add(unit);
        return unit;
    }
}
