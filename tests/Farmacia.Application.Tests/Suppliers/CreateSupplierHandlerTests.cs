using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Suppliers.CreateSupplier;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Common;

namespace Farmacia.Application.Tests.Suppliers;

public class CreateSupplierHandlerTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    private readonly InMemorySupplierRepository _suppliers = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task<Guid> CreateAsync(string taxId, Guid? pharmacyId = null) =>
        new CreateSupplierHandler(_suppliers, _unitOfWork, new FakeCurrentPharmacy(pharmacyId ?? PharmacyId)).Handle(
            new CreateSupplierCommand("Distribuidora Andina", taxId, null, null, null), CancellationToken.None);

    [Fact]
    public async Task Create_SavesTheSupplier_WithItsNormalizedNit()
    {
        var id = await CreateAsync("800.987.654-4");

        var supplier = Assert.Single(_suppliers.Items);
        Assert.Equal(id, supplier.Id);
        Assert.Equal("800987654-4", supplier.TaxId.Value);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_TheSameNitTwiceInAPharmacy_IsAConflict_EvenWrittenDifferently()
    {
        await CreateAsync("800987654-4");

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync("800.987.654-4"));
        Assert.Single(_suppliers.Items);
    }

    [Fact]
    public async Task Create_TheSameNitInAnotherPharmacy_IsAllowed()
    {
        await CreateAsync("800987654-4");

        await CreateAsync("800987654-4", pharmacyId: Guid.CreateVersion7());

        Assert.Equal(2, _suppliers.Items.Count);
    }

    [Fact]
    public async Task Create_WithAWrongVerificationDigit_IsRejectedBeforeAnyQuery()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateAsync("800987654-5"));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
