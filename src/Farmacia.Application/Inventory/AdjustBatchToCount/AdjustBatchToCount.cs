using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Inventory.AdjustBatchToCount;

/// <summary>
/// Sets a batch to what was physically counted (in base units), with the reason: damaged units, a shortage, a typing
/// mistake. The difference goes to the kardex with who did it, for the owner to review.
/// </summary>
public sealed record AdjustBatchToCountCommand(Guid BatchId, int CountedQuantity, string Reason) : IRequest;

public sealed class AdjustBatchToCountHandler(
    IBatchRepository batches,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<AdjustBatchToCountCommand>
{
    public async Task Handle(AdjustBatchToCountCommand command, CancellationToken cancellationToken)
    {
        var batch = await batches.GetOrThrowAsync(currentPharmacy.PharmacyId, command.BatchId, cancellationToken);

        batch.AdjustToCount(command.CountedQuantity, command.Reason, clock.Now, currentUser.UserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
