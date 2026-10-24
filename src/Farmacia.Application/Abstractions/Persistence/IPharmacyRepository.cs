using Farmacia.Domain.Pharmacies;

namespace Farmacia.Application.Abstractions.Persistence;

public interface IPharmacyRepository
{
    Task<Pharmacy?> GetByIdAsync(Guid pharmacyId, CancellationToken cancellationToken);
}
