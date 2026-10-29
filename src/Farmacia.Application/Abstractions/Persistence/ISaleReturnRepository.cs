using Farmacia.Domain.Sales;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ISaleReturnRepository
{
    /// <summary>Loads the whole aggregate: items with their batches.</summary>
    Task<SaleReturn?> GetByIdAsync(Guid pharmacyId, Guid saleReturnId, CancellationToken cancellationToken);

    /// <summary>A ticket's returns, oldest first, with their items and batches: what is left to return depends on them.</summary>
    Task<IReadOnlyList<SaleReturn>> ListBySaleAsync(Guid pharmacyId, Guid saleId, CancellationToken cancellationToken);

    void Add(SaleReturn saleReturn);
}
