using Farmacia.Domain.Inventory;

namespace Farmacia.Application.Abstractions.Persistence;

public interface IBatchRepository
{
    /// <summary>Its pending kardex movements are saved with it, in the same transaction.</summary>
    void Add(Batch batch);
}
