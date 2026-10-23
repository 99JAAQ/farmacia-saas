using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Farmacia.Application.Authorization;

namespace Farmacia.Api.Tests;

[Collection(ApiCollection.Name)]
public class ProductsEndpointTests(ApiFactory api)
{
    private static string NewBarcode() => $"770{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";

    private static object NewProduct(string? barcode = null, int conversionFactor = 30) => new
    {
        name = "Acetaminofén 500 mg",
        baseUnitName = "Tableta",
        requiresPrescription = false,
        saleUnits = new[] { new { name = "Caja x30", conversionFactor, price = 12_000m, barcode = barcode ?? NewBarcode() } },
    };

    [Fact]
    public async Task Health_IsPublic()
    {
        var response = await api.CreateHttpsClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithoutToken_Return401()
    {
        var client = api.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/products/{Guid.CreateVersion7()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/products", NewProduct())).StatusCode);
    }

    [Fact]
    public async Task Products_WithAForgedToken_Return401()
    {
        var client = api.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", "eyJhbGciOiJub25lIn0.eyJzdWIiOiJ4IiwicGhhcm1hY3lfaWQiOiJ4In0.");

        var response = await client.PostAsJsonAsync("/api/products", NewProduct());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_AsCashier_Returns403()
    {
        var cashier = await api.LoggedInClientAsync(api.PharmacyA, Roles.Cashier);

        var response = await cashier.PostAsJsonAsync("/api/products", NewProduct());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.StockKeeper)]
    public async Task CreateProduct_AsOwnerOrStockKeeper_Returns201_AndAnyRoleCanReadIt(string role)
    {
        var client = await api.LoggedInClientAsync(api.PharmacyA, role);

        var created = await client.PostAsJsonAsync("/api/products", NewProduct());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var cashier = await api.LoggedInClientAsync(api.PharmacyA, Roles.Cashier);
        var read = await cashier.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var product = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Acetaminofén 500 mg", product.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetProduct_OfAnotherPharmacy_Returns404()
    {
        var ownerA = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);
        var ownerB = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);
        var created = await ownerA.PostAsJsonAsync("/api/products", NewProduct());

        var response = await ownerB.GetAsync(created.Headers.Location);

        // 404, not 403: a 403 would confirm that the Id exists in another pharmacy.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SameBarcode_InTwoPharmacies_IsAllowed_ButNotTwiceInTheSamePharmacy()
    {
        var barcode = NewBarcode();
        var ownerA = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);
        var ownerB = await api.LoggedInClientAsync(api.PharmacyB, Roles.Owner);

        Assert.Equal(HttpStatusCode.Created, (await ownerA.PostAsJsonAsync("/api/products", NewProduct(barcode))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ownerB.PostAsJsonAsync("/api/products", NewProduct(barcode))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ownerA.PostAsJsonAsync("/api/products", NewProduct(barcode))).StatusCode);
    }

    [Fact]
    public async Task CreateProduct_BreakingADomainRule_Returns400_WithTheDomainMessage()
    {
        var owner = await api.LoggedInClientAsync(api.PharmacyA, Roles.Owner);

        var response = await owner.PostAsJsonAsync("/api/products", NewProduct(conversionFactor: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("El factor de conversión debe ser al menos 1.", problem.GetProperty("detail").GetString());
    }
}
