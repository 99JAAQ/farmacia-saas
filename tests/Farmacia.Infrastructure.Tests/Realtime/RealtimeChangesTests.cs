using Farmacia.Application.Abstractions.Realtime;
using Farmacia.Application.Common.Realtime;
using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;
using Farmacia.Domain.Suppliers;
using Farmacia.Infrastructure.Persistence;
using Farmacia.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Realtime;

/// <summary>The notices come from what each SaveChanges actually wrote.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class RealtimeChangesTests(SqlServerFixture fixture) : IAsyncDisposable
{
    private readonly ServiceProvider _services = InfrastructureServices.Build(fixture);

    [Fact]
    public void EveryEntityOfTheModel_SaysWhatItAnnounces()
    {
        // A new entity without an entry would change data silently: the screens showing it would not refresh.
        using var scope = _services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>().Model;

        var missing = model.GetEntityTypes()
            .Where(t => !t.IsOwned())
            .Select(t => t.ClrType)
            .Where(type => !RealtimeChangeMap.Covers(type))
            .Select(type => type.Name)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task ASavedBatch_AnnouncesTheStockOfItsProduct_AndItsKardexAddsNothing()
    {
        var product = await fixture.AddProductAsync();
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();

        db.Batches.Add(Batch.Receive(fixture.PharmacyId, product.Id, "L-RT", new DateOnly(2030, 1, 31), 30, 250m,
            new DateTimeOffset(2026, 11, 12, 9, 0, 0, SqlServerFixture.Colombia)));
        await db.SaveChangesAsync();

        Assert.Equal([new RealtimeChange(RealtimeTopics.Stock, product.Id)], Pending(scope));
    }

    [Fact]
    public async Task AFailedSave_AnnouncesNothing()
    {
        // Two suppliers with the same NIT in a pharmacy: the unique index rejects the second one.
        var nit = Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value;
        await using (var first = _services.CreateAsyncScope())
        {
            var db = first.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
            db.Suppliers.Add(Supplier.Create(fixture.PharmacyId, "Primero", nit));
            await db.SaveChangesAsync();
        }

        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();
        context.Suppliers.Add(Supplier.Create(fixture.PharmacyId, "Repetido", nit));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Empty(Pending(scope));
    }

    [Fact]
    public async Task AQuery_AnnouncesNothing()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmaciaDbContext>();

        await db.Products.AsNoTracking().Take(5).ToListAsync();
        var tracked = await db.Products.Take(1).ToListAsync();
        await db.SaveChangesAsync(); // nothing changed

        Assert.Empty(Pending(scope));
        Assert.NotNull(tracked);
    }

    private static IReadOnlyList<RealtimeChange> Pending(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<RealtimeNotifications>().Pending;

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
