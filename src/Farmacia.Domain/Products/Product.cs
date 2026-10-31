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

    /// <summary>VAT included in the sale prices. Most medicines are excluded (0%).</summary>
    public VatRate VatRate { get; private set; }

    // Referenced by Id only: Category is a separate aggregate.
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// A discontinued product is deactivated, never deleted: its sales, batches and kardex keep pointing to it.
    /// Inactive, it is neither sold nor bought; it can be reactivated.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<SaleUnit> SaleUnits => _saleUnits.AsReadOnly();

    public static Product Create(
        Guid pharmacyId,
        string name,
        string baseUnitName,
        bool requiresPrescription,
        string? activeIngredient = null,
        string? concentration = null,
        string? pharmaceuticalForm = null,
        string? sanitaryRegistration = null,
        Guid? categoryId = null,
        VatRate vatRate = VatRate.Exempt)
    {
        EnsureValid(vatRate);

        return new Product(pharmacyId)
        {
            VatRate = vatRate,
            CategoryId = categoryId,
            Name = Guard.Required(name, "El nombre del producto"),
            BaseUnitName = Guard.Required(baseUnitName, "La unidad base"),
            RequiresPrescription = requiresPrescription,
            ActiveIngredient = Guard.Optional(activeIngredient),
            Concentration = Guard.Optional(concentration),
            PharmaceuticalForm = Guard.Optional(pharmaceuticalForm),
            SanitaryRegistration = Guard.Optional(sanitaryRegistration),
        };
    }

    /// <summary>Corrects or completes the product's data. The sale units are changed one by one.</summary>
    public void UpdateDetails(
        string name,
        string baseUnitName,
        bool requiresPrescription,
        string? activeIngredient,
        string? concentration,
        string? pharmaceuticalForm,
        string? sanitaryRegistration,
        Guid? categoryId,
        VatRate vatRate)
    {
        EnsureValid(vatRate);

        Name = Guard.Required(name, "El nombre del producto");
        BaseUnitName = Guard.Required(baseUnitName, "La unidad base");
        RequiresPrescription = requiresPrescription;
        ActiveIngredient = Guard.Optional(activeIngredient);
        Concentration = Guard.Optional(concentration);
        PharmaceuticalForm = Guard.Optional(pharmaceuticalForm);
        SanitaryRegistration = Guard.Optional(sanitaryRegistration);
        CategoryId = categoryId;
        VatRate = vatRate;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void ChangeCategory(Guid? categoryId) => CategoryId = categoryId;

    public void ChangeVatRate(VatRate vatRate)
    {
        EnsureValid(vatRate);
        VatRate = vatRate;
    }

    public SaleUnit AddSaleUnit(string name, int conversionFactor, decimal price, string? barcode = null)
    {
        var unit = new SaleUnit(PharmacyId, Id, name, conversionFactor, price, barcode);
        EnsureUnique(unit.Name, unit.Barcode, except: null);

        _saleUnits.Add(unit);
        return unit;
    }

    /// <summary>
    /// Changes a presentation. A new price applies to the next sales only: every sale copied its own price. A new
    /// factor is safe too: stock is kept in base units, and past sales copied their factor.
    /// </summary>
    public void UpdateSaleUnit(Guid saleUnitId, string name, int conversionFactor, decimal price, string? barcode)
    {
        var unit = _saleUnits.SingleOrDefault(u => u.Id == saleUnitId)
            ?? throw new DomainException("La presentación no es de este producto.");

        var (validName, validBarcode) = SaleUnit.Validate(name, conversionFactor, price, barcode);
        EnsureUnique(validName, validBarcode, except: unit);

        unit.Update(validName, conversionFactor, price, validBarcode);
    }

    /// <summary>
    /// Within the product. Across products, the database's unique index on the barcode is the guard (one barcode,
    /// one presentation, in the whole pharmacy).
    /// </summary>
    private void EnsureUnique(string name, string? barcode, SaleUnit? except)
    {
        var others = _saleUnits.Where(u => u != except).ToList();

        if (others.Any(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"El producto ya tiene una unidad de venta llamada '{name}'.");

        if (barcode is not null && others.Any(u => u.Barcode == barcode))
            throw new DomainException($"El código de barras '{barcode}' ya está asignado a otra unidad de venta.");
    }

    private static void EnsureValid(VatRate vatRate)
    {
        if (!Enum.IsDefined(vatRate))
            throw new DomainException("La tarifa de IVA no es válida.");
    }
}
