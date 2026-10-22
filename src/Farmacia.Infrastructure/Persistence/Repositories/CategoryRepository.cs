using Farmacia.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(FarmaciaDbContext db) : ICategoryRepository
{
    public Task<bool> ExistsAsync(Guid pharmacyId, Guid categoryId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.PharmacyId == pharmacyId && c.Id == categoryId, cancellationToken);
}
