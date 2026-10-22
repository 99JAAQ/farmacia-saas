using Farmacia.Application.Abstractions;

namespace Farmacia.Api.Development;

/// <summary>
/// Temporary, Development only: until JWT auth exists, every request acts on the development pharmacy.
/// Its Id is set once at startup by <see cref="DevelopmentSeeder"/>.
/// </summary>
internal sealed class DevelopmentCurrentPharmacy : ICurrentPharmacy
{
    private Guid? _pharmacyId;

    public Guid PharmacyId =>
        _pharmacyId ?? throw new InvalidOperationException("The development pharmacy has not been seeded yet.");

    public void Initialize(Guid pharmacyId) => _pharmacyId = pharmacyId;
}
