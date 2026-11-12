using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Farmacia.Infrastructure.Tests;

/// <summary>
/// One disposable SQL Server container for the whole test run, with the real migrations applied.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same image as docker-compose.yml, so it is already downloaded.
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Shared by all tests: its NIT is unique across the table.</summary>
    public Guid PharmacyId { get; private set; }

    public static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var pharmacy = Pharmacy.Create("Droguería de Pruebas", "800197268-4");
        db.Pharmacies.Add(pharmacy);
        await db.SaveChangesAsync();
        PharmacyId = pharmacy.Id;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A fresh context, so assertions read from the database and not from EF's memory.</summary>
    /// <param name="session">
    /// The Row-Level Security context. By default it bypasses isolation: tests set up and check data of several
    /// pharmacies. RowLevelSecurityTests passes a pharmacy, or none, to see what a request would see.
    /// </param>
    public FarmaciaDbContext CreateContext(TenantSession? session = null) =>
        new(new DbContextOptionsBuilder<FarmaciaDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .AddInterceptors(new TenantSessionInterceptor(() => session ?? TenantSession.Bypass))
            .Options);

    public async Task<Product> AddProductAsync(string name = "Acetaminofén 500 mg")
    {
        await using var db = CreateContext();
        var product = Product.Create(PharmacyId, name, "Tableta", requiresPrescription: false);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}

[CollectionDefinition(Name)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
