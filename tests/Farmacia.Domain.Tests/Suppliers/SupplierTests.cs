using Farmacia.Domain.Common;
using Farmacia.Domain.Suppliers;

namespace Farmacia.Domain.Tests.Suppliers;

public class SupplierTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var supplier = Supplier.Create(
            PharmacyId, "Distribuidora Andina", "800987654-1",
            contactName: "Laura Gómez", email: " pedidos@andina.com ");

        Assert.Equal(PharmacyId, supplier.PharmacyId);
        Assert.Equal("Distribuidora Andina", supplier.Name);
        Assert.Equal("800987654-1", supplier.TaxId);
        Assert.Equal("Laura Gómez", supplier.ContactName);
        Assert.Equal("pedidos@andina.com", supplier.Email);
    }

    [Theory]
    [InlineData("", "800987654-1")]
    [InlineData("Distribuidora Andina", "")]
    public void Create_WithMissingRequiredData_Throws(string name, string taxId)
    {
        Assert.Throws<DomainException>(() => Supplier.Create(PharmacyId, name, taxId));
    }

    [Fact]
    public void Create_WithInvalidEmail_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Supplier.Create(PharmacyId, "Distribuidora Andina", "800987654-1", email: "no-es-un-correo"));
    }

    [Fact]
    public void Update_ChangesData_AndValidatesLikeCreate()
    {
        var supplier = Supplier.Create(PharmacyId, "Distribuidora Andina", "800987654-1");

        supplier.Update("Andina S.A.S.", "800987654-1", null, "3001234567", null);

        Assert.Equal("Andina S.A.S.", supplier.Name);
        Assert.Equal("3001234567", supplier.Phone);
        Assert.Throws<DomainException>(() => supplier.Update("", "800987654-1", null, null, null));
    }
}
