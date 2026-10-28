using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Authorization;
using Farmacia.Application.Cash.CloseCashSession;
using Farmacia.Application.Cash.OpenCashSession;
using Farmacia.Application.Cash.RecordCashMovement;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Cash;
using Farmacia.Domain.Common;
using Farmacia.Domain.Pharmacies;
using Farmacia.Infrastructure.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests.Persistence;

/// <summary>
/// The cash register against a real SQL Server: the filtered unique index (one open shift) and concurrency.
/// Each test uses its own pharmacy, because the shared one could only have one open shift for all of them.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CashPersistenceTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private Guid _pharmacyId;
    private Guid _cashierId;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        var pharmacy = Pharmacy.Create("Droguería de Caja", Nit.FromNumber(Random.Shared.Next(100_000_000, 999_999_999).ToString()).Value);
        var cashier = new ApplicationUser { FullName = "Ana Cajera", PharmacyId = pharmacy.Id };

        await using var db = fixture.CreateContext();
        db.Pharmacies.Add(pharmacy);
        db.Users.Add(cashier);
        await db.SaveChangesAsync();

        _pharmacyId = pharmacy.Id;
        _cashierId = cashier.Id;
        _services = InfrastructureServices.Build(fixture, pharmacyId: _pharmacyId, currentUser: new FixedUser(_cashierId));
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    /// <summary>One DI scope per call, like separate HTTP requests.</summary>
    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    [Fact]
    public async Task AWholeShift_IsSaved_WithItsMovements_AndTheClosingNumbers()
    {
        var id = await SendAsync(new OpenCashSessionCommand(100_000m));
        await SendAsync(new RecordCashMovementCommand(CashMovementType.Expense, 8_000m, "Domicilio"));
        await SendAsync(new RecordCashMovementCommand(CashMovementType.Income, 20_000m, "Sencillo del banco"));
        await SendAsync(new CloseCashSessionCommand(id, 110_000m, "Faltaron $2.000"));

        await using var db = fixture.CreateContext();
        var saved = await db.CashSessions.Include(s => s.Movements).SingleAsync(s => s.Id == id);

        Assert.Equal(CashSessionStatus.Closed, saved.Status);
        Assert.Equal(112_000m, saved.ExpectedCash); // 100.000 - 8.000 + 20.000 (no sales yet)
        Assert.Equal(110_000m, saved.CountedCash);
        Assert.Equal(-2_000m, saved.Difference);
        Assert.Equal(_cashierId, saved.ClosedBy);
        Assert.Equal(SqlServerFixture.Colombia, saved.OpenedAt.Offset);
        Assert.Equal(2, saved.Movements.Count);
        Assert.Equal(-8_000m, saved.Movements.Single(m => m.Type == CashMovementType.Expense).SignedAmount);
    }

    [Fact]
    public async Task TheDatabase_RejectsASecondOpenShift_WithASpecificMessage()
    {
        await SendAsync(new OpenCashSessionCommand(100_000m));

        // Skips the use case's check, as two simultaneous clicks would: only the filtered unique index is left.
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICashSessionRepository>()
            .Add(CashSession.Open(_pharmacyId, _cashierId, 50_000m, DateTimeOffset.Now));

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        Assert.Contains("turno de caja abierto", error.Message);
    }

    [Fact]
    public async Task AfterClosing_AnotherShiftCanBeOpened_BecauseTheIndexOnlyCountsOpenOnes()
    {
        var first = await SendAsync(new OpenCashSessionCommand(100_000m));
        await SendAsync(new CloseCashSessionCommand(first, 100_000m, null));
        var second = await SendAsync(new OpenCashSessionCommand(100_000m));

        await using var db = fixture.CreateContext();
        Assert.Equal(2, await db.CashSessions.CountAsync(s => s.PharmacyId == _pharmacyId));
        Assert.Equal(CashSessionStatus.Open, (await db.CashSessions.SingleAsync(s => s.Id == second)).Status);
    }

    [Fact]
    public async Task TwoClosingsAtTheSameTime_OnlyOneWins()
    {
        var id = await SendAsync(new OpenCashSessionCommand(100_000m));

        // Two requests load the same open shift before either saves (a double click on "Cerrar turno").
        await using var first = _services.CreateAsyncScope();
        await using var second = _services.CreateAsyncScope();
        var a = (await first.ServiceProvider.GetRequiredService<ICashSessionRepository>().GetByIdAsync(_pharmacyId, id, default))!;
        var b = (await second.ServiceProvider.GetRequiredService<ICashSessionRepository>().GetByIdAsync(_pharmacyId, id, default))!;
        a.Close(100_000m, 0m, _cashierId, DateTimeOffset.Now);
        b.Close(90_000m, 0m, _cashierId, DateTimeOffset.Now);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() =>
            second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());

        await using var db = fixture.CreateContext();
        Assert.Equal(100_000m, (await db.CashSessions.SingleAsync(s => s.Id == id)).CountedCash);
    }

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;

        public bool IsInRole(string role) => role == Roles.Cashier;
    }
}
