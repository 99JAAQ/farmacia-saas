using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Cash;
using MediatR;

namespace Farmacia.Application.Cash.OpenCashSession;

/// <param name="OpeningFloat">The cash in the drawer when starting, counted by the cashier who opens.</param>
public sealed record OpenCashSessionCommand(decimal OpeningFloat) : IRequest<Guid>;

public sealed class OpenCashSessionHandler(
    ICashSessionRepository sessions,
    IUserReader users,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<OpenCashSessionCommand, Guid>
{
    public async Task<Guid> Handle(OpenCashSessionCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        // One register: a clear message here; the filtered unique index is the guard against two simultaneous clicks.
        if (await sessions.GetOpenAsync(pharmacyId, cancellationToken) is { } open)
        {
            var names = await users.GetNamesAsync(pharmacyId, [open.OpenedBy], cancellationToken);
            throw new ConflictException(
                $"Ya hay un turno abierto por {names.GetValueOrDefault(open.OpenedBy, "otro usuario")} " +
                $"desde las {open.OpenedAt:HH:mm}. Debe cerrarse antes de abrir otro.");
        }

        var session = CashSession.Open(pharmacyId, currentUser.UserId, command.OpeningFloat, clock.Now);
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
