using Farmacia.Domain.Sales;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ISaleRepository
{
    /// <summary>Loads the whole aggregate: items with their batches, and payments.</summary>
    Task<Sale?> GetByIdAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken);

    void Add(Sale sale);
}
