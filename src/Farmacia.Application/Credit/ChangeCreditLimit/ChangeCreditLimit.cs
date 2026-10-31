using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Credit.ChangeCreditLimit;

/// <summary>A new limit; below the current debt it only stops new sales on credit.</summary>
public sealed record ChangeCreditLimitCommand(Guid CustomerId, decimal Limit) : IRequest;

public sealed class ChangeCreditLimitHandler(
    ICreditAccountRepository creditAccounts,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<ChangeCreditLimitCommand>
{
    public async Task Handle(ChangeCreditLimitCommand command, CancellationToken cancellationToken)
    {
        var account = await creditAccounts.GetOrThrowAsync(currentPharmacy.PharmacyId, command.CustomerId, cancellationToken);

        account.ChangeLimit(command.Limit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
