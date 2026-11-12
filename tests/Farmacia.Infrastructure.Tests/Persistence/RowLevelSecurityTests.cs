using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Persistence.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Tests.Persistence;

/// <summary>
/// The second line of tenant isolation (ADR 0014): SQL Server itself hides and protects other pharmacies' rows.
/// The queries are raw SQL on purpose, with no WHERE PharmacyId: they stand for the query someone forgets to filter.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class RowLevelSecurityTests(SqlServerFixture fixture)
{
    /// <summary>SQL Server error for a write rejected by a BLOCK predicate.</summary>
    private const int BlockPredicateViolation = 33504;

    [Fact]
    public async Task AQueryWithoutWhere_OnlySeesTheSessionsPharmacy()
    {
        var (a, b) = await TwoPharmaciesWithAProductAsync();

        await using var db = fixture.CreateContext(TenantSession.For(a.PharmacyId));
        var products = await db.Database.SqlQueryRaw<Guid>("SELECT Id AS [Value] FROM dbo.Products").ToListAsync();
        var pharmacies = await db.Database.SqlQueryRaw<Guid>("SELECT Id AS [Value] FROM dbo.Pharmacies").ToListAsync();

        Assert.Contains(a.ProductId, products);
        Assert.DoesNotContain(b.ProductId, products);
        Assert.All(await db.Products.ToListAsync(), p => Assert.Equal(a.PharmacyId, p.PharmacyId));
        Assert.Equal([a.PharmacyId], pharmacies);
    }

    [Fact]
    public async Task AConnectionWithoutPharmacy_SeesNoTenantRows()
    {
        // The login opens connections with no pharmacy: it reads users (no predicate), never business data.
        await TwoPharmaciesWithAProductAsync();

        await using var db = fixture.CreateContext(TenantSession.None);

        Assert.Equal(0, await CountAsync(db, "Products"));
        Assert.Equal(0, await CountAsync(db, "Pharmacies"));
    }

    [Fact]
    public async Task WritingARowForAnotherPharmacy_IsRejectedByTheDatabase()
    {
        var (a, b) = await TwoPharmaciesWithAProductAsync();
        var intruder = Product.Create(b.PharmacyId, "Insertado desde A", "Tableta", requiresPrescription: false);

        await using (var db = fixture.CreateContext(TenantSession.For(a.PharmacyId)))
        {
            db.Products.Add(intruder);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(BlockPredicateViolation, Assert.IsType<SqlException>(error.InnerException).Number);
        }

        await using var verify = fixture.CreateContext();
        Assert.False(await verify.Products.AnyAsync(p => p.Id == intruder.Id));
    }

    [Fact]
    public async Task MovingARowToAnotherPharmacy_IsRejected()
    {
        var (a, b) = await TwoPharmaciesWithAProductAsync();

        await using var db = fixture.CreateContext(TenantSession.For(a.PharmacyId));
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE dbo.Products SET PharmacyId = {b.PharmacyId} WHERE Id = {a.ProductId}"));

        Assert.Equal(BlockPredicateViolation, error.Number);
    }

    [Fact]
    public async Task UpdatingOrDeletingAnotherPharmacysRow_TouchesNothing()
    {
        var (a, b) = await TwoPharmaciesWithAProductAsync();

        await using (var db = fixture.CreateContext(TenantSession.For(a.PharmacyId)))
        {
            Assert.Equal(0, await db.Database.ExecuteSqlAsync($"UPDATE dbo.Products SET Name = N'Cambiado' WHERE Id = {b.ProductId}"));
            Assert.Equal(0, await db.Database.ExecuteSqlAsync($"DELETE FROM dbo.Products WHERE Id = {b.ProductId}"));
        }

        await using var verify = fixture.CreateContext();
        Assert.StartsWith("Producto RLS", (await verify.Products.SingleAsync(p => p.Id == b.ProductId)).Name);
    }

    [Theory]
    [InlineData("EXEC sys.sp_set_session_context N'PharmacyId', '00000000-0000-0000-0000-000000000001'")]
    [InlineData("EXEC sys.sp_set_session_context N'TenantBypass', 1")] // the attack if the bypass key were left unset
    public async Task InjectedSql_CannotChangeTheSessionContext(string injected)
    {
        var (a, _) = await TwoPharmaciesWithAProductAsync();

        await using var db = fixture.CreateContext(TenantSession.For(a.PharmacyId));
        await db.Database.OpenConnectionAsync(); // one connection for every command below

        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(injected));
        Assert.Equal(1, await CountAsync(db, "Pharmacies"));
    }

    [Fact]
    public async Task APooledConnection_DoesNotKeepThePreviousContext()
    {
        // A single connection in the pool: the second context is sure to reuse the first one's connection.
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            ApplicationName = "rls-pool-test",
            MaxPoolSize = 1,
        }.ConnectionString;
        await TwoPharmaciesWithAProductAsync();

        await using (var bypass = CreateContext(connectionString, TenantSession.Bypass))
            Assert.True(await CountAsync(bypass, "Pharmacies") >= 2);

        await using var anonymous = CreateContext(connectionString, TenantSession.None);
        Assert.Equal(0, await CountAsync(anonymous, "Pharmacies"));
    }

    [Fact]
    public async Task EveryTenantTable_IsCoveredByThePolicy()
    {
        // Fails when a migration creates a table with PharmacyId and forgets AddTenantIsolation.
        await using var db = fixture.CreateContext();
        var tenantTables = await db.Database.SqlQueryRaw<string>("""
            SELECT t.name AS [Value] FROM sys.tables t
            WHERE EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = 'PharmacyId')
            """).ToListAsync();
        var predicates = await db.Database.SqlQueryRaw<string>("""
            SELECT OBJECT_NAME(p.target_object_id) + ':' + p.predicate_type_desc + ISNULL(':' + p.operation_desc, '') AS [Value]
            FROM sys.security_predicates p
            JOIN sys.security_policies s ON s.object_id = p.object_id
            WHERE s.name = 'PharmacyIsolation' AND s.is_enabled = 1
            """).ToListAsync();

        // AspNetUsers stays out on purpose (ADR 0014): the login looks a user up before knowing the pharmacy.
        var expected = tenantTables.Where(t => t != "AspNetUsers").Append("Pharmacies").ToList();
        Assert.True(expected.Count > 20, "The query should find every tenant table.");
        foreach (var table in expected)
        {
            Assert.Contains($"{table}:FILTER", predicates);
            Assert.Contains($"{table}:BLOCK:AFTER INSERT", predicates);
            Assert.Contains($"{table}:BLOCK:AFTER UPDATE", predicates);
        }
    }

    private async Task<(Tenant A, Tenant B)> TwoPharmaciesWithAProductAsync()
    {
        var pharmacyA = Pharmacy.Create("Droguería RLS A", UniqueNit());
        var pharmacyB = Pharmacy.Create("Droguería RLS B", UniqueNit());
        var productA = Product.Create(pharmacyA.Id, $"Producto RLS {Guid.CreateVersion7():N}", "Tableta", false);
        var productB = Product.Create(pharmacyB.Id, $"Producto RLS {Guid.CreateVersion7():N}", "Tableta", false);

        await using var db = fixture.CreateContext(); // bypass: the setup writes two pharmacies
        db.Pharmacies.AddRange(pharmacyA, pharmacyB);
        db.Products.AddRange(productA, productB);
        await db.SaveChangesAsync();

        return (new Tenant(pharmacyA.Id, productA.Id), new Tenant(pharmacyB.Id, productB.Id));
    }

    private static string UniqueNit() => Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value;

    /// <summary>A fixed set of queries: a table name cannot be a SQL parameter.</summary>
    private static Task<int> CountAsync(FarmaciaDbContext db, string table) =>
        db.Database.SqlQueryRaw<int>(table switch
        {
            "Products" => "SELECT COUNT(*) AS [Value] FROM dbo.Products",
            "Pharmacies" => "SELECT COUNT(*) AS [Value] FROM dbo.Pharmacies",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        }).SingleAsync();

    private static FarmaciaDbContext CreateContext(string connectionString, TenantSession session) =>
        new(new DbContextOptionsBuilder<FarmaciaDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new TenantSessionInterceptor(() => session))
            .Options);

    private sealed record Tenant(Guid PharmacyId, Guid ProductId);
}
