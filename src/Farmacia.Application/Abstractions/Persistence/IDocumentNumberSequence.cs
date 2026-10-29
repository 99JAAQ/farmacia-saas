namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>The documents with a consecutive number per pharmacy. The values are stored: do not renumber them.</summary>
public enum NumberedDocument
{
    /// <summary>The sale ticket.</summary>
    Sale = 1,

    SaleReturn = 2,
}

/// <summary>The consecutive numbers of each pharmacy's documents (1, 2, 3...), one sequence per kind of document.</summary>
public interface IDocumentNumberSequence
{
    /// <summary>
    /// Reserves the next number. Call it inside a transaction (<see cref="IUnitOfWork.BeginTransactionAsync"/>):
    /// a simultaneous use case for the same document waits until this one commits, and a rollback gives the number
    /// back, so numbers never repeat nor skip.
    /// </summary>
    Task<long> NextAsync(Guid pharmacyId, NumberedDocument document, CancellationToken cancellationToken);
}
