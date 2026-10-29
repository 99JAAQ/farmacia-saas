namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>Commits all changes of a use case in a single transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A transaction that spans more than one SaveChanges or raw command (e.g. reserving a ticket number and saving
    /// the sale). Disposing it without <see cref="IUnitOfWorkTransaction.CommitAsync"/> rolls everything back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
