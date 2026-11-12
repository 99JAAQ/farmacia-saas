using Farmacia.Application.Inventory.GetStockAlerts;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Tests.Inventory;

public class StockAlertsTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateOnly Today = new(2026, 11, 12);

    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryBatchRepository _batches = new();

    private Task<StockAlertsDto> AlertsAsync() =>
        new GetStockAlertsHandler(_products, _batches, new FakeCurrentPharmacy(PharmacyId), new FakeClock(Now))
            .Handle(new GetStockAlertsQuery(), CancellationToken.None);

    private Product AddProduct(string name, int? minimum, bool active = true)
    {
        var product = Product.Create(PharmacyId, name, "Tableta", false);
        product.AddSaleUnit("Caja x30", 30, 12_000m);
        product.SetMinimumStock(minimum);
        if (!active) product.Deactivate();
        _products.Items.Add(product);
        return product;
    }

    private Batch AddBatch(Product product, DateOnly expiration, int quantity, string lot = "L1")
    {
        var batch = Batch.Receive(PharmacyId, product.Id, lot, expiration, quantity, 250m, Now.AddYears(-1));
        _batches.Items.Add(batch);
        return batch;
    }

    [Fact]
    public async Task Below_TheMinimum_TheEmptiestFirst_AndNotTheDiscontinuedNorThoseWithoutAMinimum()
    {
        var syrup = AddProduct("Jarabe", minimum: 60);
        AddBatch(syrup, new DateOnly(2028, 1, 31), 45); // 75 % of its minimum
        var tablets = AddProduct("Acetaminofén", minimum: 90);
        AddBatch(tablets, new DateOnly(2028, 1, 31), 10); // 11 %
        var enough = AddProduct("Ibuprofeno", minimum: 30);
        AddBatch(enough, new DateOnly(2028, 1, 31), 30); // exactly the minimum: no alert
        AddProduct("Descontinuado", minimum: 30, active: false);
        AddProduct("Sin mínimo", minimum: null);

        var alerts = await AlertsAsync();

        Assert.Equal(["Acetaminofén", "Jarabe"], alerts.LowStock.Select(a => a.Product.Name));
        Assert.Equal((10, 90), (alerts.LowStock[0].SellableUnits, alerts.LowStock[0].MinimumStock));
    }

    [Fact]
    public async Task ExpiredUnits_DoNotCountAsStock_TheyAreAnAlertOfTheirOwn()
    {
        var product = AddProduct("Amoxicilina", minimum: 30);
        var expired = AddBatch(product, Today.AddDays(-1), 100, "VENCIDO");

        var alerts = await AlertsAsync();

        Assert.Equal(0, Assert.Single(alerts.LowStock).SellableUnits); // 100 units, none sellable
        var alert = Assert.Single(alerts.Expired);
        Assert.Equal((expired.Id, -1), (alert.BatchId, alert.DaysLeft));
    }

    [Fact]
    public async Task ExpiringToday_CanStillBeSold_SoItIsExpiringNotExpired_AndBeyond90DaysIsNoAlert()
    {
        var product = AddProduct("Loratadina", minimum: null);
        AddBatch(product, Today, 20, "HOY");
        AddBatch(product, Today.AddDays(90), 20, "DIA-90");
        AddBatch(product, Today.AddDays(91), 20, "DIA-91");

        var alerts = await AlertsAsync();

        Assert.Empty(alerts.Expired);
        Assert.Equal([("HOY", 0), ("DIA-90", 90)], alerts.ExpiringSoon.Select(a => (a.LotNumber, a.DaysLeft)));
        Assert.Equal(2, alerts.Count);
    }
}
