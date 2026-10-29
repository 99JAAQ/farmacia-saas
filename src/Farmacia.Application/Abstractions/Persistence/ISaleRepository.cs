using Farmacia.Domain.Sales;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ISaleRepository
{
    /// <summary>Loads the whole aggregate: items with their batches, and payments.</summary>
    Task<Sale?> GetByIdAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken);

    /// <summary>By ticket number, as the cashier reads it on the customer's ticket. Loads the whole aggregate.</summary>
    Task<Sale?> GetByNumberAsync(Guid pharmacyId, long number, CancellationToken cancellationToken);

    /// <summary>A shift's sales with their items and payments, newest first.</summary>
    Task<IReadOnlyList<Sale>> ListByCashSessionAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken);

    void Add(Sale sale);
}
