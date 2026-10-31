using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(FarmaciaDbContext db) : ICategoryRepository
{
    public Task<bool> ExistsAsync(Guid pharmacyId, Guid categoryId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.PharmacyId == pharmacyId && c.Id == categoryId, cancellationToken);

    public async Task<IReadOnlyList<Category>> ListAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        await db.Categories.Where(c => c.PharmacyId == pharmacyId).OrderBy(c => c.Name).ToListAsync(cancellationToken);

    // The database collation ignores case (SQL_Latin1_General_CP1_CI_AS).
    public Task<bool> NameExistsAsync(Guid pharmacyId, string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        return db.Categories.AnyAsync(c => c.PharmacyId == pharmacyId && c.Name == trimmed, cancellationToken);
    }

    public void Add(Category category) => db.Categories.Add(category);
}
