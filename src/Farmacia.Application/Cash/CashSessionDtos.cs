using Farmacia.Domain.Cash;

namespace Farmacia.Application.Cash;

/// <param name="IsMine">True when the current user opened it: only they can sell and record movements in it.</param>
/// <param name="NetCashFromSales">Hidden (null) from the cashier while the shift is open: blind count.</param>
/// <param name="ExpectedCash">Hidden (null) from the cashier while the shift is open: blind count.</param>
public sealed record CashSessionDto(
    Guid Id,
    CashSessionStatus Status,
    Guid OpenedBy,
    string OpenedByName,
    DateTimeOffset OpenedAt,
    decimal OpeningFloat,
    bool IsMine,
    decimal? NetCashFromSales,
    decimal? ExpectedCash,
    decimal? CountedCash,
    decimal? Difference,
    string? ClosedByName,
    DateTimeOffset? ClosedAt,
    string? ClosingNotes,
    IReadOnlyList<CashMovementDto> Movements);

public sealed record CashMovementDto(
    Guid Id, CashMovementType Type, decimal Amount, string Reason, string RegisteredByName, DateTimeOffset OccurredAt);

/// <param name="Session">The open shift, or null when the register is closed.</param>
/// <param name="SuggestedOpeningFloat">What was counted when the last shift closed: what should be in the drawer now.</param>
public sealed record CurrentCashSessionDto(CashSessionDto? Session, decimal SuggestedOpeningFloat);
