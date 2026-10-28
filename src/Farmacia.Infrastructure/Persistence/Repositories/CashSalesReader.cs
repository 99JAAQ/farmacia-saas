using Farmacia.Application.Abstractions.Persistence;

namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// Sales do not exist yet (they come in the next step), so a shift has no cash from sales. When they do, this
/// adds up the shift's cash payments minus the change and the refunds; the closing already depends on it.
/// </summary>
internal sealed class CashSalesReader : ICashSalesReader
{
    public Task<decimal> GetNetCashAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(0m);
}
