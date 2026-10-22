using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Products.CreateProduct;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Common;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Tests.Products;

public class CreateProductHandlerTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryCategoryRepository _categories = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateProductHandler CreateHandler(Guid? pharmacyId = null) =>
        new(_products, _categories, _unitOfWork, new FakeCurrentPharmacy(pharmacyId ?? PharmacyId));

    private static CreateProductCommand Command(
        Guid? categoryId = null, params CreateSaleUnitItem[] saleUnits) =>
        new("Acetaminofén 500 mg", "Tableta", RequiresPrescription: false,
            ActiveIngredient: "Acetaminofén", Concentration: "500 mg", PharmaceuticalForm: "Tableta",
            SanitaryRegistration: null, categoryId,
            saleUnits.Length > 0 ? saleUnits : [new("Caja x30", 30, 12_000m, "7702057000017"), new("Tableta", 1, 500m, null)]);

    [Fact]
    public async Task Handle_ValidCommand_SavesProductWithSaleUnits_InTheCurrentPharmacy()
    {
        var id = await CreateHandler().Handle(Command(), CancellationToken.None);

        var product = Assert.Single(_products.Items);
        Assert.Equal(id, product.Id);
        Assert.Equal(PharmacyId, product.PharmacyId);
        Assert.Equal(2, product.SaleUnits.Count);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WithoutSaleUnits_Throws_AndSavesNothing()
    {
        var command = Command() with { SaleUnits = [] };

        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, CancellationToken.None));

        Assert.Empty(_products.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WithInvalidDomainData_ThrowsDomainException()
    {
        var command = Command(saleUnits: new CreateSaleUnitItem("Caja", 0, 12_000m, null));

        await Assert.ThrowsAsync<DomainException>(() => CreateHandler().Handle(command, CancellationToken.None));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_WithExistingCategory_AssignsIt()
    {
        var category = Category.Create(PharmacyId, "Analgésicos");
        _categories.Items.Add(category);

        await CreateHandler().Handle(Command(category.Id), CancellationToken.None);

        Assert.Equal(category.Id, Assert.Single(_products.Items).CategoryId);
    }

    [Fact]
    public async Task Handle_WithCategoryOfAnotherPharmacy_Throws()
    {
        var foreignCategory = Category.Create(Guid.CreateVersion7(), "Analgésicos");
        _categories.Items.Add(foreignCategory);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateHandler().Handle(Command(foreignCategory.Id), CancellationToken.None));
        Assert.Empty(_products.Items);
    }

    [Fact]
    public async Task Handle_WithBarcodeAlreadyUsed_ThrowsConflict_AndSavesNothing()
    {
        await CreateHandler().Handle(Command(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateHandler().Handle(Command() with { Name = "Otro producto" }, CancellationToken.None));

        Assert.Contains("7702057000017", ex.Message);
        Assert.Single(_products.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Handle_SameBarcodeInAnotherPharmacy_IsAllowed()
    {
        await CreateHandler(Guid.CreateVersion7()).Handle(Command(), CancellationToken.None);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.Equal(2, _products.Items.Count);
    }
}
