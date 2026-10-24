using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;
using Farmacia.Domain.Common;
using Farmacia.Domain.Products;
using Farmacia.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Api.Tests;

/// <summary>
/// Searches against the real SQL Server: accent-insensitivity depends on the database collation,
/// so it cannot be tested with in-memory fakes.
/// </summary>
[Collection(ApiCollection.Name)]
public class CatalogSearchEndpointTests(ApiFactory api)
{
    private static string Unique() => Guid.CreateVersion7().ToString("N")[..8];

    private async Task<(string Name, string Barcode)> SeedProductAsync(Guid pharmacyId, string ingredient = "Ibuprofeno")
    {
        var name = $"Acetaminofén Forte {Unique()}";
        var barcode = $"770{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        var product = Product.Create(pharmacyId, name, "Tableta", false, activeIngredient: ingredient);
        product.AddSaleUnit("Caja x30", 30, 12_000m, barcode);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return (name, barcode);
    }

    private static async Task<IEnumerable<JsonElement>> SearchAsync(HttpClient client, string url) =>
        (await client.GetFromJsonAsync<JsonElement>(url)).EnumerateArray().ToList();

    [Fact]
    public async Task ProductSearch_IgnoresAccentsAndCase_SoTheCounterCanTypeFast()
    {
        var (name, _) = await SeedProductAsync(api.PharmacyA);
        var cashier = await api.LoggedInClientAsync(api.PharmacyA, Roles.Cashier);

        // "ACETAMINOFEN" without the accent finds "Acetaminofén".
        var found = await SearchAsync(cashier, $"/api/products?search=ACETAMINOFEN FORTE {name[^8..]}");

        Assert.Equal(name, Assert.Single(found).GetProperty("name").GetString());
    }

    [Fact]
    public async Task ProductSearch_ByScannedBarcode_AndByActiveIngredient()
    {
        var ingredient = $"Ingrediente{Unique()}";
        var (name, barcode) = await SeedProductAsync(api.PharmacyA, ingredient);
        var cashier = await api.LoggedInClientAsync(api.PharmacyA, Roles.Cashier);

        Assert.Equal(name, Assert.Single(await SearchAsync(cashier, $"/api/products?search={barcode}")).GetProperty("name").GetString());
        Assert.Equal(name, Assert.Single(await SearchAsync(cashier, $"/api/products?search={ingredient.ToLowerInvariant()}")).GetProperty("name").GetString());
    }

    [Fact]
    public async Task ProductSearch_TreatsWildcardsLiterally_AndNeverShowsOtherPharmacies()
    {
        var (name, _) = await SeedProductAsync(api.PharmacyA);
        var ownerB = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);
        var ownerA = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);

        Assert.Empty(await SearchAsync(ownerB, $"/api/products?search={name[^8..]}"));
        Assert.Empty(await SearchAsync(ownerA, "/api/products?search=%25%25%25")); // "%%%" is text, not "anything"
    }

    [Fact]
    public async Task Suppliers_CanBeCreated_AndFoundByNameOrByNitWithoutItsDigit()
    {
        var owner = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);
        var number = Random.Shared.Next(100_000_000, 999_999_999).ToString();
        var nit = Nit.FromNumber(number).Value;
        var name = $"Droguería Mayorista {Unique()}";

        var created = await owner.PostAsJsonAsync("/api/suppliers", new { name, taxId = nit });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal(nit, Assert.Single(await SearchAsync(owner, $"/api/suppliers?search=drogueria mayorista {name[^8..]}")).GetProperty("taxId").GetString());
        Assert.Equal(name, Assert.Single(await SearchAsync(owner, $"/api/suppliers?search={number}")).GetProperty("name").GetString());

        var duplicate = await owner.PostAsJsonAsync("/api/suppliers", new { name = "Otro", taxId = nit });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Suppliers_AreNotForTheCashier()
    {
        var cashier = await api.LoggedInClientAsync(api.PharmacyA, Roles.Cashier);

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/api/suppliers")).StatusCode);
    }
}
