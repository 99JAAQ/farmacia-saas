using Farmacia.Domain.Common;
using Farmacia.Domain.Products;

namespace Farmacia.Domain.Tests.Products;

public class CategoryTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    [Fact]
    public void Create_WithValidName_TrimsIt()
    {
        var category = Category.Create(PharmacyId, "  Analgésicos ");

        Assert.Equal("Analgésicos", category.Name);
        Assert.Equal(PharmacyId, category.PharmacyId);
    }

    [Fact]
    public void Rename_WithEmptyName_Throws_AndKeepsPreviousName()
    {
        var category = Category.Create(PharmacyId, "Analgésicos");

        Assert.Throws<DomainException>(() => category.Rename(" "));
        Assert.Equal("Analgésicos", category.Name);
    }

    [Fact]
    public void Create_WithoutPharmacy_Throws()
    {
        Assert.Throws<DomainException>(() => Category.Create(Guid.Empty, "Analgésicos"));
    }
}
