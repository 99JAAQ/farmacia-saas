using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Products.GetProductById;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Tests.Products;

public class GetProductByIdHandlerTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    private readonly InMemoryProductRepository _products = new();

    private GetProductByIdHandler CreateHandler(Guid pharmacyId) => new(_products, new FakeCurrentPharmacy(pharmacyId));

    private Product AddProduct(Guid pharmacyId)
    {
        var product = Product.Create(pharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
        product.AddSaleUnit("Tableta", 1, 500m);
        product.AddSaleUnit("Caja x30", 30, 12_000m, "7702057000017");
        _products.Add(product);
        return product;
    }

    [Fact]
    public async Task Handle_ExistingProduct_ReturnsDto_WithLargestUnitFirst()
    {
        var product = AddProduct(PharmacyId);

        var dto = await CreateHandler(PharmacyId).Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        Assert.Equal(product.Id, dto.Id);
        Assert.Equal("Acetaminofén 500 mg", dto.Name);
        Assert.Collection(dto.SaleUnits,
            u => Assert.Equal("Caja x30", u.Name),
            u => Assert.Equal("Tableta", u.Name));
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler(PharmacyId).Handle(new GetProductByIdQuery(Guid.CreateVersion7()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProductOfAnotherPharmacy_ThrowsNotFound()
    {
        var foreignProduct = AddProduct(Guid.CreateVersion7());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateHandler(PharmacyId).Handle(new GetProductByIdQuery(foreignProduct.Id), CancellationToken.None));
    }
}
