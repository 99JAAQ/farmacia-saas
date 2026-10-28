using Farmacia.Domain.Cash;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICashSessionRepository
{
    /// <summary>Loads the whole aggregate, including its movements.</summary>
    Task<CashSession?> GetByIdAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken);

    /// <summary>The pharmacy's open shift, with its movements. There is at most one (unique index).</summary>
    Task<CashSession?> GetOpenAsync(Guid pharmacyId, CancellationToken cancellationToken);

    /// <summary>The most recently closed shift, to suggest the next opening float.</summary>
    Task<CashSession?> GetLastClosedAsync(Guid pharmacyId, CancellationToken cancellationToken);

    void Add(CashSession cashSession);
}
