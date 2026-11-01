using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Inventory;

namespace Farmacia.Application.Inventory;

internal static class BatchRepositoryExtensions
{
    /// <summary>A batch of another pharmacy is not found here either: 404, never 403.</summary>
    public static async Task<Batch> GetOrThrowAsync(
        this IBatchRepository batches, Guid pharmacyId, Guid batchId, CancellationToken cancellationToken) =>
        await batches.GetByIdAsync(pharmacyId, batchId, cancellationToken)
            ?? throw new NotFoundException("El lote no existe.");
}
