using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Tests.Fakes;

public sealed class FakeCurrentPharmacy(Guid pharmacyId) : ICurrentPharmacy
{
    public Guid PharmacyId { get; } = pharmacyId;
}

/// <summary>In-memory repository with the same tenant scoping as the real one.</summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    public List<Product> Items { get; } = [];

    public Task<Product?> GetByIdAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.PharmacyId == pharmacyId && p.Id == productId));

    public Task<IReadOnlyList<string>> GetExistingBarcodesAsync(
        Guid pharmacyId, IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> taken = Items
            .Where(p => p.PharmacyId == pharmacyId)
            .SelectMany(p => p.SaleUnits)
            .Select(u => u.Barcode)
            .OfType<string>()
            .Where(barcodes.Contains)
            .ToList();

        return Task.FromResult(taken);
    }

    public void Add(Product product) => Items.Add(product);
}

public sealed class InMemoryCategoryRepository : ICategoryRepository
{
    public List<Category> Items { get; } = [];

    public Task<bool> ExistsAsync(Guid pharmacyId, Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(c => c.PharmacyId == pharmacyId && c.Id == categoryId));
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}
