namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>Commits all changes of a use case in a single transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
