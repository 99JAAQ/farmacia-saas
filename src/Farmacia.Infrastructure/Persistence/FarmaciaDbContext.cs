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
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence;

// Identity tables live in the same context so that, e.g., a pharmacy and its owner can be created in one transaction.
public class FarmaciaDbContext(DbContextOptions<FarmaciaDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Pharmacy> Pharmacies => Set<Pharmacy>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditEntry> CreditEntries => Set<CreditEntry>();
    public DbSet<CreditPayment> CreditPayments => Set<CreditPayment>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must run first: it configures the Identity tables' keys and indexes.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FarmaciaDbContext).Assembly);

        foreach (var clrType in modelBuilder.Model.GetEntityTypes().Select(t => t.ClrType).ToList())
        {
            if (!typeof(Entity).IsAssignableFrom(clrType))
                continue;

            // Ids come from the domain (Guid v7). If EF thought they were store-generated, it would treat a new
            // child that already has an Id (e.g. a SaleUnit) as existing and issue an UPDATE instead of an INSERT.
            modelBuilder.Entity(clrType).Property(nameof(Entity.Id)).ValueGeneratedNever();

            if (typeof(TenantEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType)
                    .HasOne(typeof(Pharmacy))
                    .WithMany()
                    .HasForeignKey(nameof(TenantEntity.PharmacyId))
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Nit>().HaveConversion<NitConverter>().HaveMaxLength(20);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var clear = TrackPendingRecords();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        clear();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var clear = TrackPendingRecords();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        clear();
        return result;
    }

    /// <summary>
    /// Batch and CreditAccount record their history (kardex, credit entries) in memory; it is saved here, in the same
    /// transaction as the balance it explains. Returns what clears the pending lists once saved.
    /// </summary>
    private Action TrackPendingRecords()
    {
        var batches = ChangeTracker.Entries<Batch>()
            .Select(e => e.Entity)
            .Where(b => b.PendingMovements.Count > 0)
            .ToList();
        var accounts = ChangeTracker.Entries<CreditAccount>()
            .Select(e => e.Entity)
            .Where(a => a.PendingEntries.Count > 0)
            .ToList();

        foreach (var batch in batches)
            StockMovements.AddRange(batch.PendingMovements);
        foreach (var account in accounts)
            CreditEntries.AddRange(account.PendingEntries);

        return () =>
        {
            batches.ForEach(b => b.ClearPendingMovements());
            accounts.ForEach(a => a.ClearPendingEntries());
        };
    }
}
