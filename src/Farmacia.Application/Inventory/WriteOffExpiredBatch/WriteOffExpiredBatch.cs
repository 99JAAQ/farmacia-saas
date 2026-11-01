using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Inventory.WriteOffExpiredBatch;

/// <summary>Takes an expired batch's units out of the inventory, all of them, with a kardex entry saying who.</summary>
public sealed record WriteOffExpiredBatchCommand(Guid BatchId) : IRequest;

public sealed class WriteOffExpiredBatchHandler(
    IBatchRepository batches,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<WriteOffExpiredBatchCommand>
{
    public async Task Handle(WriteOffExpiredBatchCommand command, CancellationToken cancellationToken)
    {
        var batch = await batches.GetOrThrowAsync(currentPharmacy.PharmacyId, command.BatchId, cancellationToken);

        // The domain refuses a batch that has not expired yet (on the pharmacy's date) or that has no units.
        batch.WriteOffExpired(clock.Now, currentUser.UserId);

        // A sale taking from the same batch at the same time: one of the two gets a 409 (RowVersion).
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
