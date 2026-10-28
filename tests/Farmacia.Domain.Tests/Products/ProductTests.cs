using Farmacia.Domain.Common;
using Farmacia.Domain.Products;

namespace Farmacia.Domain.Tests.Products;

public class ProductTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    private static Product CreateAcetaminophen() =>
        Product.Create(PharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var product = Product.Create(
            PharmacyId, "  Amoxicilina 500 mg ", "Cápsula", requiresPrescription: true,
            activeIngredient: "Amoxicilina", concentration: "500 mg", sanitaryRegistration: "  ");

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(PharmacyId, product.PharmacyId);
        Assert.Equal("Amoxicilina 500 mg", product.Name);
        Assert.Equal("Cápsula", product.BaseUnitName);
        Assert.True(product.RequiresPrescription);
        Assert.Equal("Amoxicilina", product.ActiveIngredient);
        Assert.Null(product.SanitaryRegistration);
        Assert.Empty(product.SaleUnits);
    }

    [Fact]
    public void VatRate_DefaultsToExempt_AndCanBeChanged_ButNotToAnUnknownRate()
    {
        var product = CreateAcetaminophen();
        Assert.Equal(VatRate.Exempt, product.VatRate);

        product.ChangeVatRate(VatRate.Standard);
        Assert.Equal(VatRate.Standard, product.VatRate);

        Assert.Throws<DomainException>(() => product.ChangeVatRate((VatRate)7));
        Assert.Throws<DomainException>(() =>
            Product.Create(PharmacyId, "Crema", "Tubo", false, vatRate: (VatRate)7));
    }

    [Fact]
    public void ChangeCategory_SetsAndClearsCategory()
    {
        var product = CreateAcetaminophen();
        var categoryId = Guid.CreateVersion7();

        product.ChangeCategory(categoryId);
        Assert.Equal(categoryId, product.CategoryId);

        product.ChangeCategory(null);
        Assert.Null(product.CategoryId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => Product.Create(PharmacyId, name, "Tableta", false));
    }

    [Fact]
    public void Create_WithEmptyBaseUnit_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create(PharmacyId, "Acetaminofén", " ", false));
    }

    [Fact]
    public void Create_WithoutPharmacy_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create(Guid.Empty, "Acetaminofén", "Tableta", false));
    }

    [Fact]
    public void AddSaleUnit_SupportsFractionalSelling()
    {
        var product = CreateAcetaminophen();

        var box = product.AddSaleUnit("Caja x30", 30, 12_000m, barcode: "7702057000017");
        product.AddSaleUnit("Blíster", 10, 4_500m);
        product.AddSaleUnit("Tableta", 1, 500m);

        Assert.Equal(3, product.SaleUnits.Count);
        Assert.Equal(product.Id, box.ProductId);
        Assert.Equal(PharmacyId, box.PharmacyId);
        Assert.Equal(30, box.ConversionFactor);
    }

    [Fact]
    public void AddSaleUnit_WithDuplicateName_IgnoringCase_Throws()
    {
        var product = CreateAcetaminophen();
        product.AddSaleUnit("Blíster", 10, 4_500m);

        Assert.Throws<DomainException>(() => product.AddSaleUnit("BLÍSTER", 10, 4_000m));
    }

    [Fact]
    public void AddSaleUnit_WithDuplicateBarcode_Throws()
    {
        var product = CreateAcetaminophen();
        product.AddSaleUnit("Caja x30", 30, 12_000m, barcode: "7702057000017");

        Assert.Throws<DomainException>(() => product.AddSaleUnit("Caja x10", 10, 4_500m, barcode: "7702057000017"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddSaleUnit_WithConversionFactorBelowOne_Throws(int conversionFactor)
    {
        var product = CreateAcetaminophen();

        Assert.Throws<DomainException>(() => product.AddSaleUnit("Caja", conversionFactor, 12_000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void AddSaleUnit_WithNonPositivePrice_Throws(decimal price)
    {
        var product = CreateAcetaminophen();

        Assert.Throws<DomainException>(() => product.AddSaleUnit("Caja", 30, price));
    }
}
