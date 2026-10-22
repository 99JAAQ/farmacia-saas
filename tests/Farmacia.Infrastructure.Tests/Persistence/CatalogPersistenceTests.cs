using Farmacia.Domain.Customers;
using Farmacia.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class CatalogPersistenceTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AddingSaleUnit_ToAnExistingProduct_InsertsIt()
    {
        // Guards the ValueGeneratedNever setting: without it EF would issue an UPDATE for the new SaleUnit and fail.
        var product = await fixture.AddProductAsync();

        await using (var db = fixture.CreateContext())
        {
            var loaded = await db.Products.Include(p => p.SaleUnits).SingleAsync(p => p.Id == product.Id);
            loaded.AddSaleUnit("Caja x30", 30, 12_000m, barcode: $"770{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}");
            loaded.AddSaleUnit("Tableta", 1, 500m);
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var stored = await verify.Products.Include(p => p.SaleUnits).SingleAsync(p => p.Id == product.Id);
        Assert.Equal(2, stored.SaleUnits.Count);
        Assert.Contains(stored.SaleUnits, u => u.Name == "Caja x30" && u.ConversionFactor == 30 && u.Price == 12_000m);
    }

    [Fact]
    public async Task Nit_RoundTripsThroughTheDatabase()
    {
        var supplier = Supplier.Create(fixture.PharmacyId, "Distribuidora Andina", "800.987.654-4");

        await using (var db = fixture.CreateContext())
        {
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var stored = await verify.Suppliers.SingleAsync(s => s.Id == supplier.Id);
        Assert.Equal("800987654-4", stored.TaxId.Value);
        Assert.Equal(supplier.TaxId, stored.TaxId);
    }

    [Fact]
    public async Task DuplicateCustomerDocument_InTheSamePharmacy_IsRejectedByTheDatabase()
    {
        var document = Random.Shared.Next(10_000_000, 99_999_999).ToString();

        await using (var db = fixture.CreateContext())
        {
            db.Customers.Add(Customer.Create(fixture.PharmacyId, "Carlos Pérez", DocumentType.CitizenshipCard, document));
            await db.SaveChangesAsync();
        }

        await using var second = fixture.CreateContext();
        second.Customers.Add(Customer.Create(fixture.PharmacyId, "Otro Nombre", DocumentType.CitizenshipCard, document));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }
}
