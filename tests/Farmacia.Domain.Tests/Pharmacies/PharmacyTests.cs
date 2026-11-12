using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;

namespace Farmacia.Domain.Tests.Pharmacies;

public class PharmacyTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var pharmacy = Pharmacy.Create(" Droguería La Salud ", "900123456-8", address: "Calle 10 # 5-20");

        Assert.NotEqual(Guid.Empty, pharmacy.Id);
        Assert.Equal("Droguería La Salud", pharmacy.Name);
        Assert.Equal("900123456-8", pharmacy.TaxId.Value);
        Assert.Equal("Calle 10 # 5-20", pharmacy.Address);
        Assert.Null(pharmacy.Phone);
    }

    [Fact]
    public void IsVatResponsible_ByDefault_AndCanBeChanged()
    {
        var pharmacy = Pharmacy.Create("Droguería La Salud", "900123456-8");
        Assert.True(pharmacy.IsVatResponsible);

        pharmacy.SetVatResponsibility(false);

        Assert.False(pharmacy.IsVatResponsible);
    }

    [Theory]
    [InlineData("", "900123456-8")]
    [InlineData("Droguería La Salud", " ")]
    public void Create_WithMissingRequiredData_Throws(string name, string taxId)
    {
        Assert.Throws<DomainException>(() => Pharmacy.Create(name, taxId));
    }

    [Fact]
    public void UpdateDetails_ChangesHowItIsPresented_ButNotItsNit()
    {
        var pharmacy = Pharmacy.Create("Droguería", "900123456-8");

        pharmacy.UpdateDetails("  Droguería La Esperanza ", " Calle 10 ", "  ");

        Assert.Equal("Droguería La Esperanza", pharmacy.Name);
        Assert.Equal("Calle 10", pharmacy.Address);
        Assert.Null(pharmacy.Phone);
        Assert.Equal("900123456-8", pharmacy.TaxId.Value);
        Assert.Throws<DomainException>(() => pharmacy.UpdateDetails("", null, null));
    }
}
