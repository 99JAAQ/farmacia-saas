namespace Farmacia.Application.Abstractions.Persistence;

public interface ICategoryRepository
{
    Task<bool> ExistsAsync(Guid pharmacyId, Guid categoryId, CancellationToken cancellationToken);
}
