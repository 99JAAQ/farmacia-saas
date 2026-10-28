using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Cash;
using MediatR;

namespace Farmacia.Application.Cash.RecordCashMovement;

/// <summary>Cash in or out of the open shift that is not a sale (a delivery, an owner withdrawal...).</summary>
public sealed record RecordCashMovementCommand(CashMovementType Type, decimal Amount, string Reason) : IRequest<Guid>;

public sealed class RecordCashMovementHandler(
    ICashSessionRepository sessions,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RecordCashMovementCommand, Guid>
{
    public async Task<Guid> Handle(RecordCashMovementCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetOpenOrThrowAsync(currentPharmacy.PharmacyId, cancellationToken);

        // Only whoever opened the shift touches its cash: a shortage must belong to someone.
        if (session.OpenedBy != currentUser.UserId)
            throw new ForbiddenException("El turno abierto es de otro cajero: solo esa persona puede registrar movimientos en él.");

        var movement = session.RecordMovement(command.Type, command.Amount, command.Reason, currentUser.UserId, clock.Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return movement.Id;
    }
}
