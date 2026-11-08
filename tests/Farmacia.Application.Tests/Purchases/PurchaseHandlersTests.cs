using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Purchases;
using Farmacia.Application.Purchases.AddPurchaseItem;
using Farmacia.Application.Purchases.ConfirmPurchase;
using Farmacia.Application.Purchases.CreatePurchaseDraft;
using Farmacia.Application.Purchases.GetPurchaseById;
using Farmacia.Application.Purchases.ListPurchases;
using Farmacia.Application.Purchases.UpdatePurchaseHeader;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Suppliers;

namespace Farmacia.Application.Tests.Purchases;

public class PurchaseHandlersTests
{
    private static readonly DateOnly InvoiceDate = new(2026, 10, 20);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-5));

    private readonly InMemoryPurchaseRepository _purchases = new();
    private readonly InMemorySupplierRepository _suppliers = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryBatchRepository _batches = new();
    private readonly InMemoryPharmacyRepository _pharmacies = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private readonly Pharmacy _pharmacy = Pharmacy.Create("Droguería La Salud", "900123456-8");
    private readonly Supplier _supplier;
    private readonly Product _product;
    private readonly SaleUnit _box;

    public PurchaseHandlersTests()
    {
        _pharmacies.Items.Add(_pharmacy);
        _supplier = Supplier.Create(_pharmacy.Id, "Distribuidora Andina", "800987654-4");
        _suppliers.Items.Add(_supplier);
        _product = Product.Create(_pharmacy.Id, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
        _box = _product.AddSaleUnit("Caja x30", 30, 12_000m);
        _products.Add(_product);
    }

    private FakeCurrentPharmacy Current(Guid? pharmacyId = null) => new(pharmacyId ?? _pharmacy.Id);
    private PurchaseReader Reader() => new(_products, _suppliers, new FakeUserReader(), new FakeClock(Now));

    private Task<Guid> CreateDraftAsync(string invoiceNumber = "FE-100", Guid? supplierId = null) =>
        new CreatePurchaseDraftHandler(_purchases, _suppliers, _unitOfWork, Current()).Handle(
            new CreatePurchaseDraftCommand(supplierId ?? _supplier.Id, invoiceNumber, InvoiceDate, PaymentTerm.Cash, null),
            CancellationToken.None);

    private Task<Guid> AddBoxesAsync(Guid purchaseId, Guid? saleUnitId = null, Guid? pharmacyId = null, VatRate vat = VatRate.Exempt) =>
        new AddPurchaseItemHandler(_purchases, _products, _unitOfWork, Current(pharmacyId)).Handle(
            new AddPurchaseItemCommand(purchaseId, _product.Id, saleUnitId ?? _box.Id, 10, 0, 9_000m, 0m, vat, "L1",
                new DateOnly(2027, 12, 31)),
            CancellationToken.None);

    /// <summary>The stock keeper who receives the goods: their Id goes into each batch's kardex entry.</summary>
    private static readonly Guid Keeper = Guid.CreateVersion7();

    private Task<PurchaseDto> ConfirmAsync(Guid purchaseId, decimal printedTotal) =>
        new ConfirmPurchaseHandler(_purchases, _pharmacies, _batches, _unitOfWork, Current(), new FakeCurrentUser(Keeper), new FakeClock(Now), Reader())
            .Handle(new ConfirmPurchaseCommand(purchaseId, printedTotal), CancellationToken.None);

    // ---------- Draft ----------

    [Fact]
    public async Task CreateDraft_SavesIt_InTheCurrentPharmacy()
    {
        var id = await CreateDraftAsync();

        var purchase = Assert.Single(_purchases.Items);
        Assert.Equal(id, purchase.Id);
        Assert.Equal(_pharmacy.Id, purchase.PharmacyId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateDraft_WithASupplierOfAnotherPharmacy_Throws()
    {
        var foreignSupplier = Supplier.Create(Guid.CreateVersion7(), "Otro", "890903938-8");
        _suppliers.Items.Add(foreignSupplier);

        await Assert.ThrowsAsync<ValidationException>(() => CreateDraftAsync(supplierId: foreignSupplier.Id));
        Assert.Empty(_purchases.Items);
    }

    [Fact]
    public async Task CreateDraft_TheSameInvoiceOfTheSameSupplier_IgnoringCase_IsAConflict()
    {
        await CreateDraftAsync("FE-100");

        await Assert.ThrowsAsync<ConflictException>(() => CreateDraftAsync("fe-100"));

        // The same number from another supplier is a different invoice.
        var other = Supplier.Create(_pharmacy.Id, "Droguería Mayorista", "890903938-8");
        _suppliers.Items.Add(other);
        await CreateDraftAsync("FE-100", other.Id);
        Assert.Equal(2, _purchases.Items.Count);
    }

    [Fact]
    public async Task UpdateHeader_ToAnInvoiceAlreadyRegistered_IsAConflict()
    {
        await CreateDraftAsync("FE-100");
        var second = await CreateDraftAsync("FE-200");

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdatePurchaseHeaderHandler(_purchases, _suppliers, _unitOfWork, Current()).Handle(
                new UpdatePurchaseHeaderCommand(second, _supplier.Id, "FE-100", InvoiceDate, PaymentTerm.Cash, null),
                CancellationToken.None));
    }

    // ---------- Items ----------

    [Fact]
    public async Task AddItem_CopiesTheConversionFactorOfThePresentation()
    {
        var purchaseId = await CreateDraftAsync();

        await AddBoxesAsync(purchaseId);

        var item = Assert.Single(_purchases.Items.Single().Items);
        Assert.Equal(30, item.UnitsPerPackage);
        Assert.Equal(300, item.BaseUnits);
    }

    [Fact]
    public async Task AddItem_WithAPresentationOfAnotherProduct_Throws()
    {
        var purchaseId = await CreateDraftAsync();
        var otherProduct = Product.Create(_pharmacy.Id, "Ibuprofeno 400 mg", "Tableta", false);
        var otherBox = otherProduct.AddSaleUnit("Caja x20", 20, 8_000m);
        _products.Add(otherProduct);

        await Assert.ThrowsAsync<ValidationException>(() => AddBoxesAsync(purchaseId, saleUnitId: otherBox.Id));
    }

    [Fact]
    public async Task AddItem_ToAPurchaseOfAnotherPharmacy_IsNotFound()
    {
        var purchaseId = await CreateDraftAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => AddBoxesAsync(purchaseId, pharmacyId: Guid.CreateVersion7()));
    }

    // ---------- Confirm ----------

    [Fact]
    public async Task Confirm_AddsTheBatches_WithTheServerTime_AndThePharmacyVatSetting()
    {
        _pharmacy.SetVatResponsibility(false); // purchase VAT is a cost for this pharmacy
        var purchaseId = await CreateDraftAsync();
        await AddBoxesAsync(purchaseId, vat: VatRate.Standard); // 90.000 + 19% = 107.100

        var dto = await ConfirmAsync(purchaseId, 107_100m);

        var batch = Assert.Single(_batches.Items);
        Assert.Equal(300, batch.Quantity);
        Assert.Equal(357m, batch.UnitCost); // 107.100 / 300: VAT included
        Assert.Equal(Now, batch.ReceivedAt);
        Assert.Equal(PurchaseStatus.Confirmed, dto.Status);
        Assert.Equal(batch.Id, Assert.Single(dto.Items).BatchId);
    }

    [Fact]
    public async Task Confirm_AnotherPharmacysPurchase_IsNotFound()
    {
        var purchaseId = await CreateDraftAsync();
        await AddBoxesAsync(purchaseId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ConfirmPurchaseHandler(_purchases, _pharmacies, _batches, _unitOfWork, Current(Guid.CreateVersion7()),
                    new FakeCurrentUser(Keeper), new FakeClock(Now), Reader())
                .Handle(new ConfirmPurchaseCommand(purchaseId, 90_000m), CancellationToken.None));
        Assert.Empty(_batches.Items);
    }

    // ---------- Queries ----------

    [Fact]
    public async Task GetById_ReturnsProductAndSupplierNames_AndTotals()
    {
        var purchaseId = await CreateDraftAsync();
        await AddBoxesAsync(purchaseId);

        var dto = await new GetPurchaseByIdHandler(_purchases, Current(), Reader())
            .Handle(new GetPurchaseByIdQuery(purchaseId), CancellationToken.None);

        Assert.Equal("Distribuidora Andina", dto.SupplierName);
        var item = Assert.Single(dto.Items);
        Assert.Equal("Acetaminofén 500 mg", item.ProductName);
        Assert.Equal("Caja x30", item.SaleUnitName);
        Assert.Equal(90_000m, dto.Totals.Total);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var draft = await CreateDraftAsync("FE-1");
        var confirmed = await CreateDraftAsync("FE-2");
        await AddBoxesAsync(confirmed);
        await ConfirmAsync(confirmed, 90_000m);

        var drafts = await new ListPurchasesHandler(_purchases, Current(), Reader())
            .Handle(new ListPurchasesQuery(PurchaseStatus.Draft), CancellationToken.None);

        var summary = Assert.Single(drafts);
        Assert.Equal(draft, summary.Id);
        Assert.Equal("Distribuidora Andina", summary.SupplierName);
    }
}
