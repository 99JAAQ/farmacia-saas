namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>
/// What a shift's sales did to the cash in the drawer. Computed from the sales when needed, instead of being added
/// up in the shift on every sale, so offline sales synced later (phase 6) are counted too.
/// </summary>
public interface ICashSalesReader
{
    /// <summary>Cash received in the shift's sales minus the change given and the refunds paid.</summary>
    Task<decimal> GetNetCashAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken);
}
