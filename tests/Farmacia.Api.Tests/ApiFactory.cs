using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Farmacia.Application.Authorization;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Suppliers;
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Persistence.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Farmacia.Api.Tests;

/// <summary>
/// The whole API in memory, against a disposable SQL Server with the real migrations.
/// Two pharmacies (A and B) to prove tenant isolation over HTTP.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Farmacia2026";

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Guid PharmacyA { get; private set; }
    public Guid PharmacyB { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: the development seeder does not run, each test creates exactly the data it needs.
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: with minimal hosting the latter is applied too late,
        // after Program.cs has already read the connection string while registering services.
        builder.UseSetting("ConnectionStrings:Farmacia", _database.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        await using var scope = CreateSeedingScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        await db.Database.MigrateAsync();

        var pharmacyA = Pharmacy.Create("Droguería A", "800197268-4");
        var pharmacyB = Pharmacy.Create("Droguería B", "890903938-8");
        db.Pharmacies.AddRange(pharmacyA, pharmacyB);
        await db.SaveChangesAsync();
        (PharmacyA, PharmacyB) = (pharmacyA.Id, pharmacyB.Id);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
            await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// A service scope that sees every pharmacy (Row-Level Security bypassed), to set up and check test data directly
    /// in the database. Requests over HTTP still get the token's pharmacy.
    /// </summary>
    public AsyncServiceScope CreateSeedingScope()
    {
        var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantDatabaseScope>().BypassIsolation();
        return scope;
    }

    /// <summary>HTTPS base address: the refresh cookie is Secure, so it is only sent over HTTPS.</summary>
    public HttpClient CreateHttpsClient(bool handleCookies = true) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });

    /// <summary>A supplier and a product sold by boxes of 30, for purchase tests. NITs are unique per call.</summary>
    public async Task<(Guid SupplierId, Guid ProductId, Guid BoxId)> SeedCatalogAsync(Guid pharmacyId)
    {
        var supplier = Supplier.Create(
            pharmacyId, "Distribuidora Andina", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
        var product = Product.Create(pharmacyId, "Acetaminofén 500 mg", "Tableta", requiresPrescription: false);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);

        await using var scope = CreateSeedingScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        db.AddRange(supplier, product);
        await db.SaveChangesAsync();

        return (supplier.Id, product.Id, box.Id);
    }

    /// <summary>A product sold by boxes of 30 at 12.000, with one batch of the given tablets, for sales tests.</summary>
    public async Task<(Guid ProductId, Guid BoxId)> SeedProductWithStockAsync(
        Guid pharmacyId, int tablets, bool requiresPrescription = false)
    {
        var product = Product.Create(pharmacyId, $"Producto {Guid.CreateVersion7():N}", "Tableta", requiresPrescription);
        var box = product.AddSaleUnit("Caja x30", 30, 12_000m);
        var batch = Batch.Receive(pharmacyId, product.Id, "L1", new DateOnly(2030, 12, 31), tablets, 250m,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-5)));

        await using var scope = CreateSeedingScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        db.AddRange(product, batch);
        await db.SaveChangesAsync();

        return (product.Id, box.Id);
    }

    /// <summary>A batch received on 1 Jan 2026 (so it can already be expired today), in base units.</summary>
    public async Task SeedBatchAsync(Guid pharmacyId, Guid productId, string lot, DateOnly expiration, int quantity)
    {
        var batch = Batch.Receive(pharmacyId, productId, lot, expiration, quantity, 250m,
            new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5)));

        await using var scope = CreateSeedingScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A pharmacy of its own, for tests that need the register: each pharmacy has a single open shift, so tests
    /// sharing pharmacy A would block each other.
    /// </summary>
    public async Task<Guid> CreatePharmacyAsync()
    {
        var pharmacy = Pharmacy.Create(
            "Droguería de Caja", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);

        await using var scope = CreateSeedingScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        db.Pharmacies.Add(pharmacy);
        await db.SaveChangesAsync();

        return pharmacy.Id;
    }

    public async Task<string> CreateUserAsync(Guid pharmacyId, string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.CreateVersion7():N}@farmacia.test";

        await using var scope = CreateSeedingScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = $"Usuario {role}", PharmacyId = pharmacyId };

        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded)
            throw new InvalidOperationException("Could not create the test user.");

        return email;
    }

    /// <summary>A client that sends the access token of a new user with the given role, as the web app will.</summary>
    public async Task<HttpClient> LoggedInClientAsync(Guid pharmacyId, string role)
    {
        var email = await CreateUserAsync(pharmacyId, role);
        var client = CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private sealed record SessionDto(string AccessToken);
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
