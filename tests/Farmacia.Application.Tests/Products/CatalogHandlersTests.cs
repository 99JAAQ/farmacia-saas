using Farmacia.Application.Categories.CreateCategory;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Products.GetCatalog;
using Farmacia.Application.Products.SearchProducts;
using Farmacia.Application.Products.SetProductActive;
using Farmacia.Application.Products.UpdateSaleUnit;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Tests.Products;

public class CatalogHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryBatchRepository _batches = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentPharmacy _pharmacy;

    public CatalogHandlersTests() => _pharmacy = new FakeCurrentPharmacy(_pharmacyId);

    private Product AddProduct(string name, string? barcode = null)
    {
        var product = Product.Create(_pharmacyId, name, "Tableta", false);
        product.AddSaleUnit("Caja x30", 30, 12_000m, barcode);
        _products.Add(product);
        return product;
    }

    [Fact]
    public async Task Catalog_CountsSellableAndExpiredStock_WithThePharmacysDate()
    {
        var product = AddProduct("Loratadina");
        var received = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));
        _batches.Add(Batch.Receive(_pharmacyId, product.Id, "HOY", new DateOnly(2026, 10, 31), 10, 250m, received)); // still sellable today
        _batches.Add(Batch.Receive(_pharmacyId, product.Id, "AYER", new DateOnly(2026, 10, 30), 5, 250m, received));
        _batches.Add(Batch.Receive(_pharmacyId, product.Id, "LUEGO", new DateOnly(2027, 5, 31), 20, 250m, received));

        var item = Assert.Single(await new GetCatalogHandler(_products, _batches, _pharmacy, new FakeClock(Now))
            .Handle(new GetCatalogQuery(null, IncludeInactive: false), CancellationToken.None));

        Assert.Equal((30, 5, new DateOnly(2026, 10, 31)), (item.SellableUnits, item.ExpiredUnits, item.NextExpiration));
    }

    [Fact]
    public async Task Search_LeavesOutDiscontinuedProducts_UnlessAsked()
    {
        var product = AddProduct("Jarabe");
        await new SetProductActiveHandler(_products, _unitOfWork, _pharmacy).Handle(new SetProductActiveCommand(product.Id, false), CancellationToken.None);
        var search = new SearchProductsHandler(_products, _pharmacy);

        Assert.Empty(await search.Handle(new SearchProductsQuery("Jarabe"), CancellationToken.None));
        Assert.False(Assert.Single(await search.Handle(new SearchProductsQuery("Jarabe", IncludeInactive: true), CancellationToken.None)).IsActive);
    }

    [Fact]
    public async Task UpdateSaleUnit_WithAnotherProductsBarcode_IsAConflict_ButItsOwnIsFine()
    {
        AddProduct("Ibuprofeno", barcode: "7700000000111");
        var naproxen = AddProduct("Naproxeno", barcode: "7700000000222");
        var box = naproxen.SaleUnits.Single();
        var handler = new UpdateSaleUnitHandler(_products, _unitOfWork, _pharmacy);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new UpdateSaleUnitCommand(naproxen.Id, box.Id, "Caja x30", 30, 12_000m, "7700000000111"), CancellationToken.None));

        await handler.Handle(new UpdateSaleUnitCommand(naproxen.Id, box.Id, "Caja x30", 30, 12_500m, "7700000000222"), CancellationToken.None);
        Assert.Equal(12_500m, box.Price);
    }

    [Fact]
    public async Task ACategory_IsUniqueIgnoringCase()
    {
        var handler = new CreateCategoryHandler(_categories, _unitOfWork, _pharmacy);
        await handler.Handle(new CreateCategoryCommand("Analgésicos"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateCategoryCommand(" analgésicos "), CancellationToken.None));
    }
}
