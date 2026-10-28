using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Cash.CloseCashSession;

/// <summary>
/// Closes a shift with the cash counted by hand. The id goes in the request (not "the open one") so a delayed
/// double click cannot close the next cashier's shift.
/// </summary>
/// <returns>The closed shift, now with the expected cash and the difference.</returns>
public sealed record CloseCashSessionCommand(Guid CashSessionId, decimal CountedCash, string? Notes) : IRequest<CashSessionDto>;

public sealed class CloseCashSessionHandler(
    ICashSessionRepository sessions,
    ICashSalesReader cashSales,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock,
    CashSessionReader reader) : IRequestHandler<CloseCashSessionCommand, CashSessionDto>
{
    public async Task<CashSessionDto> Handle(CloseCashSessionCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var session = await sessions.GetOrThrowAsync(pharmacyId, command.CashSessionId, cancellationToken);

        // Each cashier closes their own shift; the owner can close a forgotten one (ClosedBy records who did).
        if (session.OpenedBy != currentUser.UserId && !currentUser.IsInRole(Roles.Owner))
            throw new ForbiddenException("Solo quien abrió el turno, o el dueño, puede cerrarlo.");

        var netCashFromSales = await cashSales.GetNetCashAsync(pharmacyId, session.Id, cancellationToken);
        session.Close(command.CountedCash, netCashFromSales, currentUser.UserId, clock.Now, command.Notes);

        // A second close running at the same time fails here with a 409 (RowVersion on the shift).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await reader.ToDtoAsync(session, cancellationToken);
    }
}
