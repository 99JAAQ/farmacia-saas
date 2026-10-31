using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Cash;
using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Sales;
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
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken, bool includeInactive = false) =>
        Task.FromResult<IReadOnlyList<Product>>(Items
            .Where(p => p.PharmacyId == pharmacyId && (includeInactive || p.IsActive))
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

    public Task<IReadOnlyList<Category>> ListAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Category>>(Items.Where(c => c.PharmacyId == pharmacyId).OrderBy(c => c.Name).ToList());

    public Task<bool> NameExistsAsync(Guid pharmacyId, string name, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(c => c.PharmacyId == pharmacyId && string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));

    public void Add(Category category) => Items.Add(category);
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

    public Task<IReadOnlyList<Batch>> GetInStockAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Batch>>(
            Items.Where(b => b.PharmacyId == pharmacyId && productIds.Contains(b.ProductId) && b.Quantity > 0).ToList());

    public Task<IReadOnlyList<Batch>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Batch>>(Items.Where(b => b.PharmacyId == pharmacyId && batchIds.Contains(b.Id)).ToList());

    public Task<IReadOnlyList<ProductStock>> GetStockAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> productIds, DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductStock>>(Items
            .Where(b => b.PharmacyId == pharmacyId && productIds.Contains(b.ProductId) && b.Quantity > 0)
            .GroupBy(b => b.ProductId)
            .Select(g => new ProductStock(
                g.Key,
                g.Where(b => !b.IsExpiredOn(today)).Sum(b => b.Quantity),
                g.Where(b => b.IsExpiredOn(today)).Sum(b => b.Quantity),
                g.Where(b => !b.IsExpiredOn(today)).Min(b => (DateOnly?)b.ExpirationDate)))
            .ToList());

    public Task<IReadOnlyList<Batch>> ListInStockByProductAsync(Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Batch>>(Items
            .Where(b => b.PharmacyId == pharmacyId && b.ProductId == productId && b.Quantity > 0)
            .OrderBy(b => b.ExpirationDate)
            .ToList());

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
    public int CommitCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(this));

    private sealed class FakeTransaction(FakeUnitOfWork owner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            owner.CommitCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
{
    public Guid UserId { get; } = userId;

    public bool IsInRole(string role) => roles.Contains(role);
}

public sealed class InMemoryCashSessionRepository : ICashSessionRepository
{
    public List<CashSession> Items { get; } = [];

    public Task<CashSession?> GetByIdAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(s => s.PharmacyId == pharmacyId && s.Id == cashSessionId));

    public Task<CashSession?> GetOpenAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(s => s.PharmacyId == pharmacyId && s.Status == CashSessionStatus.Open));

    public Task<CashSession?> GetLastClosedAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        Task.FromResult(Items
            .Where(s => s.PharmacyId == pharmacyId && s.Status == CashSessionStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefault());

    public void Add(CashSession cashSession) => Items.Add(cashSession);
}

/// <summary>The shift's net cash from sales, set by the test.</summary>
public sealed class FakeCashSalesReader : ICashSalesReader
{
    public decimal NetCash { get; set; }

    public Task<decimal> GetNetCashAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(NetCash);
}

public sealed class FakeUserReader : IUserReader
{
    public Dictionary<Guid, (Guid PharmacyId, string Name)> Users { get; } = [];

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Users
            .Where(u => u.Value.PharmacyId == pharmacyId && userIds.Contains(u.Key))
            .ToDictionary(u => u.Key, u => u.Value.Name));
}

public sealed class InMemorySaleRepository : ISaleRepository
{
    public List<Sale> Items { get; } = [];

    public Task<Sale?> GetByIdAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(s => s.PharmacyId == pharmacyId && s.Id == saleId));

    public Task<Sale?> GetByNumberAsync(Guid pharmacyId, long number, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(s => s.PharmacyId == pharmacyId && s.Number == number));

    public Task<IReadOnlyList<Sale>> ListByCashSessionAsync(
        Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Sale>>(Items
            .Where(s => s.PharmacyId == pharmacyId && s.CashSessionId == cashSessionId)
            .OrderByDescending(s => s.Number)
            .ToList());

    public void Add(Sale sale) => Items.Add(sale);
}

/// <summary>Consecutive per pharmacy and document, like the real one (without its locking: that is tested against SQL Server).</summary>
public sealed class FakeDocumentNumberSequence : IDocumentNumberSequence
{
    private readonly Dictionary<(Guid, NumberedDocument), long> _last = [];

    public Task<long> NextAsync(Guid pharmacyId, NumberedDocument document, CancellationToken cancellationToken)
    {
        _last[(pharmacyId, document)] = _last.GetValueOrDefault((pharmacyId, document)) + 1;
        return Task.FromResult(_last[(pharmacyId, document)]);
    }
}

public sealed class InMemorySaleReturnRepository : ISaleReturnRepository
{
    public List<SaleReturn> Items { get; } = [];

    public Task<SaleReturn?> GetByIdAsync(Guid pharmacyId, Guid saleReturnId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(r => r.PharmacyId == pharmacyId && r.Id == saleReturnId));

    public Task<IReadOnlyList<SaleReturn>> ListBySaleAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SaleReturn>>(Items
            .Where(r => r.PharmacyId == pharmacyId && r.SaleId == saleId)
            .OrderBy(r => r.Number)
            .ToList());

    public void Add(SaleReturn saleReturn) => Items.Add(saleReturn);
}

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    public List<Customer> Items { get; } = [];

    public Task<Customer?> GetByIdAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(c => c.PharmacyId == pharmacyId && c.Id == customerId));

    public Task<IReadOnlyList<Customer>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(Items.Where(c => c.PharmacyId == pharmacyId && customerIds.Contains(c.Id)).ToList());

    /// <summary>Plain "contains" on the name: accent-insensitivity is tested against SQL Server.</summary>
    public Task<IReadOnlyList<Customer>> SearchAsync(Guid pharmacyId, string? search, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(Items
            .Where(c => c.PharmacyId == pharmacyId
                        && (string.IsNullOrWhiteSpace(search)
                            || c.FullName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                            || c.DocumentNumber == search.Trim()))
            .OrderBy(c => c.FullName)
            .Take(take)
            .ToList());

    public Task<bool> DocumentExistsAsync(
        Guid pharmacyId, DocumentType documentType, string documentNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(c => c.PharmacyId == pharmacyId && c.DocumentType == documentType && c.DocumentNumber == documentNumber));

    public void Add(Customer customer) => Items.Add(customer);
}

public sealed class InMemoryCreditAccountRepository : ICreditAccountRepository
{
    public List<CreditAccount> Items { get; } = [];

    public Task<CreditAccount?> GetByCustomerAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(a => a.PharmacyId == pharmacyId && a.CustomerId == customerId));

    public Task<IReadOnlyList<CreditAccount>> GetByCustomersAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CreditAccount>>(Items.Where(a => a.PharmacyId == pharmacyId && customerIds.Contains(a.CustomerId)).ToList());

    public Task<IReadOnlyList<CreditAccount>> ListDebtorsAsync(Guid pharmacyId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CreditAccount>>(Items
            .Where(a => a.PharmacyId == pharmacyId && a.Balance > 0)
            .OrderBy(a => a.DebtSince)
            .Take(take)
            .ToList());

    /// <summary>The real repository reads saved entries; here they are still pending on the account, in order.</summary>
    public Task<IReadOnlyList<CreditEntry>> ListEntriesAsync(
        Guid pharmacyId, Guid creditAccountId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CreditEntry>>(Items
            .Where(a => a.PharmacyId == pharmacyId && a.Id == creditAccountId)
            .SelectMany(a => a.PendingEntries)
            .Reverse()
            .Take(take)
            .ToList());

    public void Add(CreditAccount account) => Items.Add(account);
}

public sealed class InMemoryCreditPaymentRepository : ICreditPaymentRepository
{
    public List<CreditPayment> Items { get; } = [];

    public Task<CreditPayment?> GetByIdAsync(Guid pharmacyId, Guid creditPaymentId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(p => p.PharmacyId == pharmacyId && p.Id == creditPaymentId));

    public void Add(CreditPayment payment) => Items.Add(payment);
}
