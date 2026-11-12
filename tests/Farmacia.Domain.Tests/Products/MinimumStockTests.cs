using Farmacia.Domain.Common;
using Farmacia.Domain.Products;

namespace Farmacia.Domain.Tests.Products;

public class MinimumStockTests
{
    private static Product Tablets() => Product.Create(Guid.CreateVersion7(), "Acetaminofén 500 mg", "Tableta", false);

    [Fact]
    public void BelowTheMinimum_IsAnAlert_AtTheMinimumItIsNot()
    {
        var product = Tablets();
        product.SetMinimumStock(60); // 2 boxes of 30

        Assert.True(product.IsBelowMinimum(59));
        Assert.False(product.IsBelowMinimum(60));
    }

    [Fact]
    public void WithoutAMinimum_ThereIsNoAlert_AndZeroMeansNone()
    {
        var product = Tablets();
        Assert.False(product.IsBelowMinimum(0));

        product.SetMinimumStock(0);
        Assert.Null(product.MinimumStock);
    }

    [Fact]
    public void ADiscontinuedProduct_IsNotAnAlert_ItWillNotBeBoughtAgain()
    {
        var product = Tablets();
        product.SetMinimumStock(60);
        product.Deactivate();

        Assert.False(product.IsBelowMinimum(0));
    }

    [Fact]
    public void ANegativeMinimum_IsRejected() =>
        Assert.Throws<DomainException>(() => Tablets().SetMinimumStock(-1));
}
