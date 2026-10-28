using Farmacia.Application.Authorization;
using Farmacia.Application.Cash;
using Farmacia.Application.Cash.CloseCashSession;
using Farmacia.Application.Cash.GetCurrentCashSession;
using Farmacia.Application.Cash.OpenCashSession;
using Farmacia.Application.Cash.RecordCashMovement;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Cash;

namespace Farmacia.Application.Tests.Cash;

public class CashSessionHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 7, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly Guid _ana = Guid.CreateVersion7();    // cashier, morning
    private readonly Guid _beto = Guid.CreateVersion7();   // cashier, afternoon
    private readonly Guid _owner = Guid.CreateVersion7();

    private readonly InMemoryCashSessionRepository _sessions = new();
    private readonly FakeCashSalesReader _cashSales = new();
    private readonly FakeUserReader _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    public CashSessionHandlersTests()
    {
        _users.Users[_ana] = (_pharmacyId, "Ana Cajera");
        _users.Users[_beto] = (_pharmacyId, "Beto Cajero");
        _users.Users[_owner] = (_pharmacyId, "Dueña");
    }

    private FakeCurrentUser Cashier(Guid userId) => new(userId, Roles.Cashier);
    private FakeCurrentUser Owner() => new(_owner, Roles.Owner);
    private FakeCurrentPharmacy Pharmacy(Guid? pharmacyId = null) => new(pharmacyId ?? _pharmacyId);
    private CashSessionReader Reader(FakeCurrentUser user) => new(_cashSales, _users, user);

    private Task<Guid> OpenAsync(FakeCurrentUser user, decimal openingFloat = 100_000m) =>
        new OpenCashSessionHandler(_sessions, _users, _unitOfWork, Pharmacy(), user, new FakeClock(Now))
            .Handle(new OpenCashSessionCommand(openingFloat), CancellationToken.None);

    private Task<Guid> RecordAsync(FakeCurrentUser user, CashMovementType type = CashMovementType.Expense, decimal amount = 8_000m) =>
        new RecordCashMovementHandler(_sessions, _unitOfWork, Pharmacy(), user, new FakeClock(Now.AddHours(1)))
            .Handle(new RecordCashMovementCommand(type, amount, "Domicilio"), CancellationToken.None);

    private Task<CashSessionDto> CloseAsync(FakeCurrentUser user, Guid sessionId, decimal counted, Guid? pharmacyId = null) =>
        new CloseCashSessionHandler(_sessions, _cashSales, _unitOfWork, Pharmacy(pharmacyId), user, new FakeClock(Now.AddHours(8)), Reader(user))
            .Handle(new CloseCashSessionCommand(sessionId, counted, null), CancellationToken.None);

    private Task<CurrentCashSessionDto> CurrentAsync(FakeCurrentUser user) =>
        new GetCurrentCashSessionHandler(_sessions, Pharmacy(), Reader(user))
            .Handle(new GetCurrentCashSessionQuery(), CancellationToken.None);

    // ---------- Opening ----------

    [Fact]
    public async Task Open_SavesTheShift_OpenedByTheCurrentUser_AtServerTime()
    {
        var id = await OpenAsync(Cashier(_ana));

        var session = Assert.Single(_sessions.Items);
        Assert.Equal(id, session.Id);
        Assert.Equal(_ana, session.OpenedBy);
        Assert.Equal(Now, session.OpenedAt);
        Assert.Equal(_pharmacyId, session.PharmacyId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Open_WhileAnotherShiftIsOpen_IsAConflict_ThatNamesWhoHasIt()
    {
        await OpenAsync(Cashier(_ana));

        var error = await Assert.ThrowsAsync<ConflictException>(() => OpenAsync(Cashier(_beto)));

        Assert.Contains("Ana Cajera", error.Message);
        Assert.Single(_sessions.Items);
    }

    // ---------- Current shift ----------

    [Fact]
    public async Task Current_WithTheRegisterClosed_SuggestsWhatWasCountedLastTime()
    {
        var first = await OpenAsync(Cashier(_ana));
        await CloseAsync(Cashier(_ana), first, counted: 95_000m);

        var current = await CurrentAsync(Cashier(_beto));

        Assert.Null(current.Session);
        Assert.Equal(95_000m, current.SuggestedOpeningFloat);
    }

    [Fact]
    public async Task Current_BlindCount_TheCashierDoesNotSeeTheExpectedCash_ButTheOwnerDoes()
    {
        await OpenAsync(Cashier(_ana));
        await RecordAsync(Cashier(_ana), amount: 8_000m);
        _cashSales.NetCash = 350_000m;

        var asCashier = (await CurrentAsync(Cashier(_ana))).Session!;
        var asOwner = (await CurrentAsync(Owner())).Session!;

        Assert.True(asCashier.IsMine);
        Assert.Null(asCashier.ExpectedCash);
        Assert.Null(asCashier.NetCashFromSales);
        Assert.Equal("Ana Cajera", asCashier.OpenedByName);
        Assert.Equal("Ana Cajera", Assert.Single(asCashier.Movements).RegisteredByName);

        Assert.False(asOwner.IsMine);
        Assert.Equal(350_000m, asOwner.NetCashFromSales);
        Assert.Equal(442_000m, asOwner.ExpectedCash); // 100.000 - 8.000 + 350.000
    }

    // ---------- Movements ----------

    [Fact]
    public async Task Record_WithoutAnOpenShift_IsAConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => RecordAsync(Cashier(_ana)));
    }

    [Fact]
    public async Task Record_InSomeoneElsesShift_IsForbidden_EvenForTheOwner()
    {
        await OpenAsync(Cashier(_ana));

        await Assert.ThrowsAsync<ForbiddenException>(() => RecordAsync(Cashier(_beto)));
        await Assert.ThrowsAsync<ForbiddenException>(() => RecordAsync(Owner()));
        Assert.Empty(_sessions.Items.Single().Movements);
    }

    // ---------- Closing ----------

    [Fact]
    public async Task Close_UsesTheNetCashOfTheSales_AndRevealsTheDifference()
    {
        var id = await OpenAsync(Cashier(_ana));
        await RecordAsync(Cashier(_ana), amount: 8_000m);
        _cashSales.NetCash = 350_000m;

        var closed = await CloseAsync(Cashier(_ana), id, counted: 440_000m);

        Assert.Equal(CashSessionStatus.Closed, closed.Status);
        Assert.Equal(442_000m, closed.ExpectedCash);
        Assert.Equal(-2_000m, closed.Difference); // shortage
        Assert.Equal("Ana Cajera", closed.ClosedByName);
    }

    [Fact]
    public async Task Close_AnotherCashiersShift_IsForbidden()
    {
        var id = await OpenAsync(Cashier(_ana));

        await Assert.ThrowsAsync<ForbiddenException>(() => CloseAsync(Cashier(_beto), id, 100_000m));
        Assert.Equal(CashSessionStatus.Open, _sessions.Items.Single().Status);
    }

    [Fact]
    public async Task Close_TheOwnerCanCloseAForgottenShift_AndItRecordsWhoClosedIt()
    {
        var id = await OpenAsync(Cashier(_ana));

        var closed = await CloseAsync(Owner(), id, 100_000m);

        Assert.Equal("Ana Cajera", closed.OpenedByName);
        Assert.Equal("Dueña", closed.ClosedByName);
        Assert.Equal(_owner, _sessions.Items.Single().ClosedBy);
    }

    [Fact]
    public async Task Close_AShiftOfAnotherPharmacy_IsNotFound()
    {
        var id = await OpenAsync(Cashier(_ana));

        await Assert.ThrowsAsync<NotFoundException>(() => CloseAsync(Owner(), id, 100_000m, pharmacyId: Guid.CreateVersion7()));
    }
}
