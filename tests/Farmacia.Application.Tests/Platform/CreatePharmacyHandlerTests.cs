using Farmacia.Application.Abstractions;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Platform;
using Farmacia.Application.Platform.CreatePharmacy;
using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;

namespace Farmacia.Application.Tests.Platform;

public class CreatePharmacyHandlerTests
{
    private readonly RecordingPlatform _platform = new();

    private Task<Guid> CreateAsync(
        string taxId = "900123456-8", string ownerName = "Ana Gómez", string ownerEmail = " ana@drogueria.co ", bool vat = true) =>
        new CreatePharmacyHandler(_platform).Handle(
            new CreatePharmacyCommand("Droguería La 10", taxId, "Calle 10 # 5-20", null, vat, ownerName, ownerEmail, "Farmacia2026"),
            CancellationToken.None);

    [Fact]
    public async Task Create_PassesThePharmacyAndItsOwner_Together()
    {
        var id = await CreateAsync(vat: false);

        var (pharmacy, owner) = Assert.Single(_platform.Created);
        Assert.Equal(id, pharmacy.Id);
        Assert.Equal("900123456-8", pharmacy.TaxId.Value);
        Assert.False(pharmacy.IsVatResponsible);
        Assert.Equal(new NewOwner("Ana Gómez", "ana@drogueria.co", "Farmacia2026"), owner);
    }

    [Fact]
    public async Task Create_WithAWrongVerificationDigit_FailsBeforeWritingAnything()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateAsync(taxId: "900123456-1"));
        Assert.Empty(_platform.Created);
    }

    [Theory]
    [InlineData("", "ana@drogueria.co")]
    [InlineData("Ana Gómez", "  ")]
    public async Task Create_WithoutTheOwnersNameOrEmail_IsInvalid(string name, string email)
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateAsync(ownerName: name, ownerEmail: email));
        Assert.Empty(_platform.Created);
    }

    private sealed class RecordingPlatform : IPlatformPharmacies
    {
        public List<(Pharmacy Pharmacy, NewOwner Owner)> Created { get; } = [];

        public Task CreateAsync(Pharmacy pharmacy, NewOwner owner, CancellationToken cancellationToken)
        {
            Created.Add((pharmacy, owner));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlatformPharmacyDto>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlatformPharmacyDto>>([]);
    }
}
