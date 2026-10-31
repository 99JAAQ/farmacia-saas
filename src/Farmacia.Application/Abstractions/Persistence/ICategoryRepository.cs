using Farmacia.Domain.Products;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICategoryRepository
{
    Task<bool> ExistsAsync(Guid pharmacyId, Guid categoryId, CancellationToken cancellationToken);

    /// <summary>Alphabetically.</summary>
    Task<IReadOnlyList<Category>> ListAsync(Guid pharmacyId, CancellationToken cancellationToken);

    /// <summary>Ignoring case: "Analgésicos" and "analgésicos" are the same category.</summary>
    Task<bool> NameExistsAsync(Guid pharmacyId, string name, CancellationToken cancellationToken);

    void Add(Category category);
}
