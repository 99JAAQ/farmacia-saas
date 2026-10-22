using Farmacia.Domain.Common;
using Farmacia.Domain.Customers;

namespace Farmacia.Domain.Tests.Customers;

public class CustomerTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var customer = Customer.Create(
            PharmacyId, " Carlos Pérez ", DocumentType.CitizenshipCard, "1020304050", phone: "  ");

        Assert.Equal(PharmacyId, customer.PharmacyId);
        Assert.Equal("Carlos Pérez", customer.FullName);
        Assert.Equal(DocumentType.CitizenshipCard, customer.DocumentType);
        Assert.Equal("1020304050", customer.DocumentNumber);
        Assert.Null(customer.Phone);
        Assert.Null(customer.Email);
    }

    [Theory]
    [InlineData("", "1020304050")]
    [InlineData("Carlos Pérez", " ")]
    public void Create_WithMissingRequiredData_Throws(string fullName, string documentNumber)
    {
        Assert.Throws<DomainException>(() =>
            Customer.Create(PharmacyId, fullName, DocumentType.CitizenshipCard, documentNumber));
    }

    [Fact]
    public void Create_WithUndefinedDocumentType_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Customer.Create(PharmacyId, "Carlos Pérez", (DocumentType)99, "1020304050"));
    }

    [Fact]
    public void Create_WithInvalidEmail_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Customer.Create(PharmacyId, "Carlos Pérez", DocumentType.CitizenshipCard, "1020304050", email: "carlos@"));
    }

    [Fact]
    public void DocumentType_UsesDianCodes()
    {
        Assert.Equal(13, (int)DocumentType.CitizenshipCard);
        Assert.Equal(31, (int)DocumentType.Nit);
    }
}
