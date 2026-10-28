using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Authorization;
using Farmacia.Domain.Cash;

namespace Farmacia.Application.Cash;

/// <summary>
/// Builds the DTO with the users' names and applies the blind count: while the shift is open, only the owner sees
/// how much cash is expected. If the cashier knew the number, they would "count" until it matched and a real
/// shortage would be hidden.
/// </summary>
public sealed class CashSessionReader(ICashSalesReader cashSales, IUserReader users, ICurrentUser currentUser)
{
    public async Task<CashSessionDto> ToDtoAsync(CashSession session, CancellationToken cancellationToken)
    {
        var userIds = session.Movements.Select(m => m.RegisteredBy)
            .Append(session.OpenedBy)
            .Concat(session.ClosedBy is { } closedBy ? [closedBy] : [])
            .Distinct()
            .ToList();
        var names = await users.GetNamesAsync(session.PharmacyId, userIds, cancellationToken);
        string NameOf(Guid userId) => names.GetValueOrDefault(userId, "Usuario desconocido");

        decimal? netCashFromSales = null;
        var expectedCash = session.ExpectedCash; // stored when closing; null while open

        if (session.Status == CashSessionStatus.Open && currentUser.IsInRole(Roles.Owner))
        {
            netCashFromSales = await cashSales.GetNetCashAsync(session.PharmacyId, session.Id, cancellationToken);
            expectedCash = session.CalculateExpectedCash(netCashFromSales.Value);
        }

        return new CashSessionDto(
            session.Id,
            session.Status,
            session.OpenedBy,
            NameOf(session.OpenedBy),
            session.OpenedAt,
            session.OpeningFloat,
            IsMine: session.OpenedBy == currentUser.UserId,
            netCashFromSales,
            expectedCash,
            session.CountedCash,
            session.Difference,
            session.ClosedBy is { } closer ? NameOf(closer) : null,
            session.ClosedAt,
            session.ClosingNotes,
            session.Movements
                .OrderBy(m => m.OccurredAt)
                .Select(m => new CashMovementDto(m.Id, m.Type, m.Amount, m.Reason, NameOf(m.RegisteredBy), m.OccurredAt))
                .ToList());
    }
}
