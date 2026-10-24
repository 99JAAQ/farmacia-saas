using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Pharmacies;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class PharmacyRepository(FarmaciaDbContext db) : IPharmacyRepository
{
    public async Task<Pharmacy?> GetByIdAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        await db.Pharmacies.FindAsync([pharmacyId], cancellationToken);
}
