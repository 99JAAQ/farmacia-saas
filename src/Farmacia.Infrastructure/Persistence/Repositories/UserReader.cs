using Farmacia.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class UserReader(FarmaciaDbContext db) : IUserReader
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await db.Users
            .Where(u => u.PharmacyId == pharmacyId && userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
}
