using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Abstractions.Persistence;

public interface IPurchaseRepository
{
    /// <summary>Loads the whole aggregate, including its items.</summary>
    Task<Purchase?> GetByIdAsync(Guid pharmacyId, Guid purchaseId, CancellationToken cancellationToken);

    /// <summary>Most recent first (by invoice date), with their items.</summary>
    Task<IReadOnlyList<Purchase>> ListAsync(
        Guid pharmacyId, PurchaseStatus? status, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Confirmed credit invoices (whole aggregates), optionally of one supplier. The ones already paid are filtered out
    /// by the caller: the total lives in the lines, so the database cannot filter by balance (fine at a pharmacy's scale).
    /// </summary>
    Task<IReadOnlyList<Purchase>> ListCreditInvoicesAsync(Guid pharmacyId, Guid? supplierId, CancellationToken cancellationToken);

    /// <param name="invoiceNumber">Already normalized by the domain.</param>
    /// <param name="exceptPurchaseId">The purchase being checked, so it does not collide with itself.</param>
    Task<bool> InvoiceExistsAsync(
        Guid pharmacyId, Guid supplierId, string invoiceNumber, Guid exceptPurchaseId, CancellationToken cancellationToken);

    void Add(Purchase purchase);
}
