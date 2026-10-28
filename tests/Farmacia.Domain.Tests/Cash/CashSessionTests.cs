using Farmacia.Domain.Cash;
using Farmacia.Domain.Common;

namespace Farmacia.Domain.Tests.Cash;

public class CashSessionTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid CashierId = Guid.CreateVersion7();
    private static readonly DateTimeOffset OpenedAt = new(2026, 10, 27, 7, 0, 0, TimeSpan.FromHours(-5));

    private static CashSession OpenShift(decimal openingFloat = 100_000m) =>
        CashSession.Open(PharmacyId, CashierId, openingFloat, OpenedAt);

    // ---------- Opening ----------

    [Fact]
    public void Open_StartsOpen_WithTheFloat_AndNoClosingData()
    {
        var shift = OpenShift();

        Assert.Equal(CashSessionStatus.Open, shift.Status);
        Assert.Equal(100_000m, shift.OpeningFloat);
        Assert.Equal(CashierId, shift.OpenedBy);
        Assert.Null(shift.ExpectedCash);
        Assert.Null(shift.CountedCash);
        Assert.Null(shift.Difference);
    }

    [Fact]
    public void Open_WithAZeroFloat_IsAllowed()
    {
        Assert.Equal(0m, OpenShift(0m).OpeningFloat);
    }

    [Fact]
    public void Open_WithANegativeFloat_OrWithoutCashier_Throws()
    {
        Assert.Throws<DomainException>(() => OpenShift(-1m));
        Assert.Throws<DomainException>(() => CashSession.Open(PharmacyId, Guid.Empty, 0m, OpenedAt));
    }

    // ---------- Movements ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-5_000)]
    public void RecordMovement_WithoutAPositiveAmount_Throws(decimal amount)
    {
        Assert.Throws<DomainException>(() =>
            OpenShift().RecordMovement(CashMovementType.Expense, amount, "Domicilio", CashierId, OpenedAt.AddHours(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordMovement_WithoutAReason_Throws(string reason)
    {
        Assert.Throws<DomainException>(() =>
            OpenShift().RecordMovement(CashMovementType.Expense, 5_000m, reason, CashierId, OpenedAt.AddHours(1)));
    }

    [Fact]
    public void RecordMovement_BeforeTheOpening_Throws()
    {
        Assert.Throws<DomainException>(() =>
            OpenShift().RecordMovement(CashMovementType.Income, 5_000m, "Sencillo", CashierId, OpenedAt.AddMinutes(-1)));
    }

    [Fact]
    public void RecordMovement_KeepsTheAmountPositive_AndTheSignInSignedAmount()
    {
        var shift = OpenShift();

        var income = shift.RecordMovement(CashMovementType.Income, 20_000m, " Sencillo del banco ", CashierId, OpenedAt.AddHours(1));
        var expense = shift.RecordMovement(CashMovementType.Expense, 8_000m, "Domicilio", CashierId, OpenedAt.AddHours(2));

        Assert.Equal("Sencillo del banco", income.Reason);
        Assert.Equal(20_000m, income.SignedAmount);
        Assert.Equal(8_000m, expense.Amount);
        Assert.Equal(-8_000m, expense.SignedAmount);
        Assert.Equal(shift.Id, expense.CashSessionId);
        Assert.Equal(2, shift.Movements.Count);
    }

    // ---------- Closing ----------

    [Fact]
    public void ExpectedCash_IsFloat_PlusIncome_MinusExpenses_PlusNetCashFromSales()
    {
        var shift = OpenShift(100_000m);
        shift.RecordMovement(CashMovementType.Income, 20_000m, "Sencillo", CashierId, OpenedAt.AddHours(1));
        shift.RecordMovement(CashMovementType.Expense, 8_000m, "Domicilio", CashierId, OpenedAt.AddHours(2));

        // 100.000 + 20.000 - 8.000 + 350.000
        Assert.Equal(462_000m, shift.CalculateExpectedCash(netCashFromSales: 350_000m));
    }

    [Theory]
    [InlineData(462_000, 0)]        // exact
    [InlineData(460_000, -2_000)]   // shortage
    [InlineData(462_500, 500)]      // surplus
    public void Close_StoresExpectedAndCounted_AndTheDifference(decimal counted, decimal difference)
    {
        var shift = OpenShift(100_000m);
        shift.RecordMovement(CashMovementType.Income, 20_000m, "Sencillo", CashierId, OpenedAt.AddHours(1));
        shift.RecordMovement(CashMovementType.Expense, 8_000m, "Domicilio", CashierId, OpenedAt.AddHours(2));
        var closedAt = OpenedAt.AddHours(8);

        shift.Close(counted, netCashFromSales: 350_000m, CashierId, closedAt, notes: "  ");

        Assert.Equal(CashSessionStatus.Closed, shift.Status);
        Assert.Equal(462_000m, shift.ExpectedCash);
        Assert.Equal(counted, shift.CountedCash);
        Assert.Equal(difference, shift.Difference);
        Assert.Equal(closedAt, shift.ClosedAt);
        Assert.Equal(CashierId, shift.ClosedBy);
        Assert.Null(shift.ClosingNotes);
    }

    [Fact]
    public void Close_WithMoreRefundsThanCashSales_CanHaveANegativeNetCash()
    {
        var shift = OpenShift(100_000m);

        shift.Close(70_000m, netCashFromSales: -30_000m, CashierId, OpenedAt.AddHours(8));

        Assert.Equal(70_000m, shift.ExpectedCash);
        Assert.Equal(0m, shift.Difference);
    }

    [Fact]
    public void Close_WithInvalidData_Throws_AndLeavesTheShiftOpen()
    {
        var shift = OpenShift();

        Assert.Throws<DomainException>(() => shift.Close(-1m, 0m, CashierId, OpenedAt.AddHours(8)));
        Assert.Throws<DomainException>(() => shift.Close(0m, 0m, Guid.Empty, OpenedAt.AddHours(8)));
        Assert.Throws<DomainException>(() => shift.Close(0m, 0m, CashierId, OpenedAt.AddMinutes(-1)));

        Assert.Equal(CashSessionStatus.Open, shift.Status);
        Assert.Null(shift.ExpectedCash);
    }

    [Fact]
    public void AClosedShift_CannotBeClosedAgain_NorReceiveMovements()
    {
        var shift = OpenShift();
        shift.Close(100_000m, 0m, CashierId, OpenedAt.AddHours(8), "Sin novedad");

        Assert.Equal("Sin novedad", shift.ClosingNotes);
        Assert.Throws<DomainException>(() => shift.Close(100_000m, 0m, CashierId, OpenedAt.AddHours(9)));
        Assert.Throws<DomainException>(() =>
            shift.RecordMovement(CashMovementType.Expense, 5_000m, "Domicilio", CashierId, OpenedAt.AddHours(9)));
    }
}
