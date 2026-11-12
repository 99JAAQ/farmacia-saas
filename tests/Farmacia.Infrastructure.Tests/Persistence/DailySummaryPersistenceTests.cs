using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Cash.OpenCashSession;
using Farmacia.Application.Dashboard;
using Farmacia.Application.Sales.RegisterSale;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Sales;
using Farmacia.Infrastructure.Identity;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Farmacia.Infrastructure.Tests.Persistence;

/// <summary>The day's summary against SQL Server, with a fake clock to sell at chosen hours.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class DailySummaryPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    // 23:30 in Colombia on 11 Nov is already 12 Nov in UTC (04:30Z).
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 23, 30, 0, SqlServerFixture.Colombia));
    private Guid _pharmacyId;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        var pharmacy = Pharmacy.Create("Droguería del Panel", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
        var cashier = new ApplicationUser { FullName = "Ana Cajera", PharmacyId = pharmacy.Id };
        await using (var db = fixture.CreateContext())
        {
            db.Pharmacies.Add(pharmacy);
            db.Users.Add(cashier);
            await db.SaveChangesAsync();
        }

        _pharmacyId = pharmacy.Id;
        _services = InfrastructureServices.Build(fixture, _time, _pharmacyId, new FixedUser(cashier.Id));
        await SendAsync(new OpenCashSessionCommand(0m));
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task TheDayIsColombias_ASaleAt2330IsYesterday_EvenIfItIsAlreadyTomorrowInUtc()
    {
        var (product, box) = await SeedProductAsync(VatRate.Standard);
        await SellAsync(product, box, boxes: 1); // 23:30 on the 11th
        _time.Advance(TimeSpan.FromHours(1)); // 00:30 on the 12th
        await SellAsync(product, box, boxes: 2, PaymentMethod.Card);

        var today = await SendAsync(new GetDailySummaryQuery());

        Assert.Equal(new DateOnly(2026, 11, 12), today.Date);
        Assert.Equal(1, today.SalesCount);
        Assert.Equal(23_800m, today.GrossSales); // 2 × 11.900
        Assert.Equal(3_800m, today.Vat); // 19 % included: 23.800 × 19 / 119
        Assert.Equal(15_000m, today.Cost); // 60 tablets × 250
        Assert.Equal(5_000m, today.GrossProfit); // 23.800 − 3.800 − 15.000
        var hour = Assert.Single(today.ByHour);
        Assert.Equal((0, 23_800m), (hour.Hour, hour.Total));
        Assert.Equal(PaymentMethod.Card, Assert.Single(today.ByPaymentMethod).Method);
        var top = Assert.Single(today.TopProducts);
        Assert.Equal((product.Name, 60), (top.Name, top.BaseUnits));
    }

    private async Task<(Product Product, SaleUnit Box)> SeedProductAsync(VatRate vat)
    {
        var product = Product.Create(_pharmacyId, $"Jarabe {Guid.CreateVersion7():N}", "Tableta", false, vatRate: vat);
        var box = product.AddSaleUnit("Caja x30", 30, 11_900m);
        var batch = Batch.Receive(_pharmacyId, product.Id, "L1", new DateOnly(2030, 12, 31), 300, 250m,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, SqlServerFixture.Colombia));
        await using var db = fixture.CreateContext();
        db.AddRange(product, batch);
        await db.SaveChangesAsync();
        return (product, box);
    }

    private Task SellAsync(Product product, SaleUnit box, int boxes, PaymentMethod method = PaymentMethod.Cash) =>
        SendAsync(new RegisterSaleCommand(
            [new RegisterSaleLine(product.Id, box.Id, boxes)],
            [new RegisterSalePayment(method, boxes * box.Price)],
            PrescriptionChecked: false));

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public bool IsInRole(string role) => role == Roles.Owner;
    }
}
