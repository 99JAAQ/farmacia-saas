using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Abstractions.Persistence;

public interface IPurchaseRepository
{
    /// <summary>Loads the whole aggregate, including its items.</summary>
    Task<Purchase?> GetByIdAsync(Guid pharmacyId, Guid purchaseId, CancellationToken cancellationToken);

    /// <summary>Most recent first (by invoice date), with their items.</summary>
    Task<IReadOnlyList<Purchase>> ListAsync(
        Guid pharmacyId, PurchaseStatus? status, int take, CancellationToken cancellationToken);

    /// <param name="invoiceNumber">Already normalized by the domain.</param>
    /// <param name="exceptPurchaseId">The purchase being checked, so it does not collide with itself.</param>
    Task<bool> InvoiceExistsAsync(
        Guid pharmacyId, Guid supplierId, string invoiceNumber, Guid exceptPurchaseId, CancellationToken cancellationToken);

    void Add(Purchase purchase);
}
