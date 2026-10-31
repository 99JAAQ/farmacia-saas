using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Credit.SetCreditSuspended;

/// <summary>Suspends or resumes a customer's credit. Suspended, the account takes no new sales but still takes payments.</summary>
public sealed record SetCreditSuspendedCommand(Guid CustomerId, bool Suspended) : IRequest;

public sealed class SetCreditSuspendedHandler(
    ICreditAccountRepository creditAccounts,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<SetCreditSuspendedCommand>
{
    public async Task Handle(SetCreditSuspendedCommand command, CancellationToken cancellationToken)
    {
        var account = await creditAccounts.GetOrThrowAsync(currentPharmacy.PharmacyId, command.CustomerId, cancellationToken);

        if (command.Suspended)
            account.Suspend();
        else
            account.Resume();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
