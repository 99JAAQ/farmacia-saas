using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Suppliers;

namespace Farmacia.Application.Tests.Fakes;

public sealed class FakeCurrentPharmacy(Guid pharmacyId) : ICurrentPharmacy
{
    public Guid PharmacyId { get; } = pharmacyId;
}

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; } = now;
}

/// <summary>In-memory repository with the same tenant scoping as the real one.</summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    public List<Product> Items { get; } = [];

    public Task<Product?> GetByIdAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.PharmacyId == pharmacyId && p.Id == productId));

    public Task<IReadOnlyList<Product>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>(
            Items.Where(p => p.PharmacyId == pharmacyId && productIds.Contains(p.Id)).ToList());

    // Plain case-insensitive "contains": accent-insensitivity depends on SQL Server and is tested in the API tests.
    public Task<IReadOnlyList<Product>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>(Items
            .Where(p => p.PharmacyId == pharmacyId)
            .Where(p => string.IsNullOrWhiteSpace(search) || p.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .Take(take)
            .ToList());

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

public sealed class InMemoryPurchaseRepository : IPurchaseRepository
{
    public List<Purchase> Items { get; } = [];

    public Task<Purchase?> GetByIdAsync(Guid pharmacyId, Guid purchaseId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.PharmacyId == pharmacyId && p.Id == purchaseId));

    public Task<IReadOnlyList<Purchase>> ListAsync(
        Guid pharmacyId, PurchaseStatus? status, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Purchase>>(Items
            .Where(p => p.PharmacyId == pharmacyId && (status == null || p.Status == status))
            .OrderByDescending(p => p.InvoiceDate)
            .Take(take)
            .ToList());

    public Task<bool> InvoiceExistsAsync(
        Guid pharmacyId, Guid supplierId, string invoiceNumber, Guid exceptPurchaseId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(p => p.PharmacyId == pharmacyId && p.SupplierId == supplierId
                                       && p.InvoiceNumber == invoiceNumber && p.Id != exceptPurchaseId));

    public void Add(Purchase purchase) => Items.Add(purchase);
}

public sealed class InMemorySupplierRepository : ISupplierRepository
{
    public List<Supplier> Items { get; } = [];

    public Task<bool> ExistsAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(s => s.PharmacyId == pharmacyId && s.Id == supplierId));

    public Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Supplier>>(
            Items.Where(s => s.PharmacyId == pharmacyId && supplierIds.Contains(s.Id)).ToList());

    public Task<Supplier?> GetByIdAsync(Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(s => s.PharmacyId == pharmacyId && s.Id == supplierId));

    public Task<IReadOnlyList<Supplier>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Items
            .Where(s => s.PharmacyId == pharmacyId)
            .Where(s => string.IsNullOrWhiteSpace(search) || s.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .Take(take)
            .ToList());

    public Task<bool> TaxIdExistsAsync(Guid pharmacyId, Nit taxId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(s => s.PharmacyId == pharmacyId && s.TaxId == taxId));

    public void Add(Supplier supplier) => Items.Add(supplier);
}

public sealed class InMemoryBatchRepository : IBatchRepository
{
    public List<Batch> Items { get; } = [];

    public void Add(Batch batch) => Items.Add(batch);
}

public sealed class InMemoryPharmacyRepository : IPharmacyRepository
{
    public List<Pharmacy> Items { get; } = [];

    public Task<Pharmacy?> GetByIdAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Id == pharmacyId));
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
