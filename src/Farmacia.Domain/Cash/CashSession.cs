using Farmacia.Domain.Common;

namespace Farmacia.Domain.Cash;

/// <summary>
/// Aggregate root: one cashier's shift at the register. Opens with a float, records the cash that comes in or goes
/// out without being a sale, and closes with the cash counted by hand; the difference is the shift's surplus or
/// shortage.
/// </summary>
/// <remarks>
/// The shift does not add up its sales one by one (every sale would update this row and fight over it). When closing,
/// the use case computes the net cash of the shift's sales and passes it in. Only one open shift per pharmacy is
/// enforced by the database, the only place that sees two simultaneous clicks.
/// </remarks>
public class CashSession : TenantEntity
{
    private readonly List<CashMovement> _movements = [];

    private CashSession() { } // EF Core

    private CashSession(Guid pharmacyId) : base(pharmacyId) { }

    public Guid OpenedBy { get; private init; }
    public DateTimeOffset OpenedAt { get; private init; }

    /// <summary>Cash in the drawer when the shift starts (the change to give back).</summary>
    public decimal OpeningFloat { get; private init; }

    public CashSessionStatus Status { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>What should be in the drawer when closing, kept so the closing does not change if data changes later.</summary>
    public decimal? ExpectedCash { get; private set; }

    /// <summary>What the cashier counted by hand.</summary>
    public decimal? CountedCash { get; private set; }

    public string? ClosingNotes { get; private set; }

    public IReadOnlyCollection<CashMovement> Movements => _movements.AsReadOnly();

    /// <summary>Counted minus expected: positive is a surplus, negative a shortage. Null while open.</summary>
    public decimal? Difference => CountedCash - ExpectedCash;

    /// <param name="openedAt">Local Colombian time.</param>
    public static CashSession Open(Guid pharmacyId, Guid openedBy, decimal openingFloat, DateTimeOffset openedAt)
    {
        if (openedBy == Guid.Empty)
            throw new DomainException("El cajero que abre el turno es obligatorio.");

        if (openingFloat < 0)
            throw new DomainException("La base inicial no puede ser negativa.");

        return new CashSession(pharmacyId)
        {
            OpenedBy = openedBy,
            OpenedAt = openedAt,
            OpeningFloat = openingFloat,
            Status = CashSessionStatus.Open,
        };
    }

    public CashMovement RecordMovement(
        CashMovementType type, decimal amount, string reason, Guid registeredBy, DateTimeOffset occurredAt)
    {
        EnsureOpen();
        EnsureNotBeforeOpening(occurredAt);

        var movement = new CashMovement(PharmacyId, Id, type, amount, reason, registeredBy, occurredAt);
        _movements.Add(movement);
        return movement;
    }

    /// <summary>Float plus income minus expenses plus the net cash of the sales (cash received minus change and refunds).</summary>
    public decimal CalculateExpectedCash(decimal netCashFromSales) =>
        OpeningFloat + _movements.Sum(m => m.SignedAmount) + netCashFromSales;

    /// <param name="countedCash">The cash counted by hand.</param>
    /// <param name="netCashFromSales">Cash received in the shift's sales minus the change given and the refunds paid.</param>
    /// <param name="closedAt">Local Colombian time.</param>
    public void Close(
        decimal countedCash, decimal netCashFromSales, Guid closedBy, DateTimeOffset closedAt, string? notes = null)
    {
        EnsureOpen();
        EnsureNotBeforeOpening(closedAt);

        if (countedCash < 0)
            throw new DomainException("El efectivo contado no puede ser negativo.");

        if (closedBy == Guid.Empty)
            throw new DomainException("El usuario que cierra el turno es obligatorio.");

        ExpectedCash = CalculateExpectedCash(netCashFromSales);
        CountedCash = countedCash;
        ClosedBy = closedBy;
        ClosedAt = closedAt;
        ClosingNotes = Guard.Optional(notes);
        Status = CashSessionStatus.Closed;
    }

    private void EnsureOpen()
    {
        if (Status != CashSessionStatus.Open)
            throw new DomainException("El turno de caja ya está cerrado.");
    }

    private void EnsureNotBeforeOpening(DateTimeOffset timestamp)
    {
        if (timestamp < OpenedAt)
            throw new DomainException("La fecha no puede ser anterior a la apertura del turno.");
    }
}
