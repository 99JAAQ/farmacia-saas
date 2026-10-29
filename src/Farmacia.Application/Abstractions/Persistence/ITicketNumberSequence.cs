namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>The consecutive ticket numbers of each pharmacy (1, 2, 3...).</summary>
public interface ITicketNumberSequence
{
    /// <summary>
    /// Reserves the next number. Call it inside a transaction (<see cref="IUnitOfWork.BeginTransactionAsync"/>):
    /// a simultaneous sale waits until this one commits, and a rollback gives the number back, so tickets never
    /// repeat nor skip numbers.
    /// </summary>
    Task<long> NextAsync(Guid pharmacyId, CancellationToken cancellationToken);
}
