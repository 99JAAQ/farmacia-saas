using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;

namespace Farmacia.Domain.Tests.Pharmacies;

public class PharmacyTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var pharmacy = Pharmacy.Create(" Droguería La Salud ", "900123456-7", address: "Calle 10 # 5-20");

        Assert.NotEqual(Guid.Empty, pharmacy.Id);
        Assert.Equal("Droguería La Salud", pharmacy.Name);
        Assert.Equal("900123456-7", pharmacy.TaxId);
        Assert.Equal("Calle 10 # 5-20", pharmacy.Address);
        Assert.Null(pharmacy.Phone);
    }

    [Theory]
    [InlineData("", "900123456-7")]
    [InlineData("Droguería La Salud", " ")]
    public void Create_WithMissingRequiredData_Throws(string name, string taxId)
    {
        Assert.Throws<DomainException>(() => Pharmacy.Create(name, taxId));
    }
}
