using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Tests.Products;

/// <summary>Keeping the catalog up to date: editing a product and its presentations, and retiring it.</summary>
public class ProductCatalogTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly Product _product = Product.Create(PharmacyId, "Acetaminofen 500", "Tableta", requiresPrescription: false);
    private readonly SaleUnit _box;
    private readonly SaleUnit _blister;

    public ProductCatalogTests()
    {
        _box = _product.AddSaleUnit("Caja x30", 30, 12_000m, "7702057000017");
        _blister = _product.AddSaleUnit("Blíster x10", 10, 4_500m);
    }

    [Fact]
    public void UpdateDetails_CorrectsTheData_AndKeepsThePresentations()
    {
        var category = Guid.CreateVersion7();

        _product.UpdateDetails(" Acetaminofén 500 mg ", "Tableta", true, "Acetaminofén", "500 mg", "Tableta", "INVIMA 2020M-001", category, VatRate.Standard);

        Assert.Equal(("Acetaminofén 500 mg", true, "INVIMA 2020M-001"), (_product.Name, _product.RequiresPrescription, _product.SanitaryRegistration));
        Assert.Equal((category, VatRate.Standard), (_product.CategoryId, _product.VatRate));
        Assert.Equal(2, _product.SaleUnits.Count);
    }

    [Fact]
    public void UpdateDetails_RejectsAMissingName_OrAnInvalidVatRate()
    {
        Assert.Throws<DomainException>(() => _product.UpdateDetails(" ", "Tableta", false, null, null, null, null, null, VatRate.Exempt));
        Assert.Throws<DomainException>(() => _product.UpdateDetails("X", "Tableta", false, null, null, null, null, null, (VatRate)7));
        Assert.Equal("Acetaminofen 500", _product.Name);
    }

    [Fact]
    public void UpdateSaleUnit_ChangesPriceFactorNameAndBarcode()
    {
        _product.UpdateSaleUnit(_box.Id, "Caja x20", 20, 9_000m, " 7702057000099 ");

        Assert.Equal(("Caja x20", 20, 9_000m, "7702057000099"), (_box.Name, _box.ConversionFactor, _box.Price, _box.Barcode));
    }

    [Fact]
    public void UpdateSaleUnit_CanKeepItsOwnNameAndBarcode_ButNotTakeAnotherUnits()
    {
        _product.UpdateSaleUnit(_box.Id, "Caja x30", 30, 12_500m, "7702057000017"); // same name and code: fine
        Assert.Equal(12_500m, _box.Price);

        Assert.Throws<DomainException>(() => _product.UpdateSaleUnit(_blister.Id, "caja x30", 10, 4_500m, null));
        Assert.Throws<DomainException>(() => _product.UpdateSaleUnit(_blister.Id, "Blíster x10", 10, 4_500m, "7702057000017"));
        Assert.Equal(("Blíster x10", null), (_blister.Name, _blister.Barcode));
    }

    [Theory]
    [InlineData("", 10, 4_500)]
    [InlineData("Blíster", 0, 4_500)]
    [InlineData("Blíster", 10, 0)]
    public void UpdateSaleUnit_RejectsInvalidValues_LeavingTheUnitAsItWas(string name, int factor, decimal price)
    {
        Assert.Throws<DomainException>(() => _product.UpdateSaleUnit(_blister.Id, name, factor, price, null));
        Assert.Equal((10, 4_500m), (_blister.ConversionFactor, _blister.Price));
    }

    [Fact]
    public void UpdateSaleUnit_OfAnotherProduct_IsRejected()
    {
        var other = Product.Create(PharmacyId, "Ibuprofeno", "Tableta", false).AddSaleUnit("Caja", 20, 8_000m);

        Assert.Throws<DomainException>(() => _product.UpdateSaleUnit(other.Id, "Caja", 20, 8_000m, null));
    }

    [Fact]
    public void AnInactiveProduct_IsNotSold_UntilItIsReactivated()
    {
        var batch = Batch.Receive(PharmacyId, _product.Id, "L1", new DateOnly(2027, 6, 30), 90, 250m, Now.AddDays(-5));
        Sale Sell() => Sale.Register(PharmacyId, 1, Guid.CreateVersion7(), Guid.CreateVersion7(), Now,
            [new SaleLine(_product, _box.Id, 1, [batch])], [new PaymentLine(PaymentMethod.Cash, 12_000m)], false);

        _product.Deactivate();
        Assert.False(_product.IsActive);
        Assert.Contains("inactivo", Assert.Throws<DomainException>(Sell).Message);
        Assert.Equal(90, batch.Quantity);

        _product.Activate();
        Sell();
        Assert.Equal(60, batch.Quantity);
    }
}
