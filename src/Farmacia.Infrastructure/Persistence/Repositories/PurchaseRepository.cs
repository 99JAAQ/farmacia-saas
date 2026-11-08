using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Purchases;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseRepository(FarmaciaDbContext db) : IPurchaseRepository
{
    public Task<Purchase?> GetByIdAsync(Guid pharmacyId, Guid purchaseId, CancellationToken cancellationToken) =>
        db.Purchases
            .Include(p => p.Items)
            .Include(p => p.Payments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.PharmacyId == pharmacyId && p.Id == purchaseId, cancellationToken);

    public async Task<IReadOnlyList<Purchase>> ListAsync(
        Guid pharmacyId, PurchaseStatus? status, int take, CancellationToken cancellationToken) =>
        await db.Purchases
            .Include(p => p.Items)
            .Include(p => p.Payments)
            .Where(p => p.PharmacyId == pharmacyId && (status == null || p.Status == status))
            .OrderByDescending(p => p.InvoiceDate)
            .ThenByDescending(p => p.Id) // Guid v7: later creation sorts later
            .Take(take)
            .AsSplitQuery() // avoids repeating each purchase row once per item
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Purchase>> ListCreditInvoicesAsync(
        Guid pharmacyId, Guid? supplierId, CancellationToken cancellationToken) =>
        await db.Purchases
            .Include(p => p.Items)
            .Include(p => p.Payments)
            .Where(p => p.PharmacyId == pharmacyId
                        && p.PaymentTerm == PaymentTerm.Credit
                        && p.Status == PurchaseStatus.Confirmed
                        && (supplierId == null || p.SupplierId == supplierId))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<bool> InvoiceExistsAsync(
        Guid pharmacyId, Guid supplierId, string invoiceNumber, Guid exceptPurchaseId, CancellationToken cancellationToken) =>
        db.Purchases.AnyAsync(
            p => p.PharmacyId == pharmacyId && p.SupplierId == supplierId && p.InvoiceNumber == invoiceNumber
                 && p.Id != exceptPurchaseId,
            cancellationToken);

    public void Add(Purchase purchase) => db.Purchases.Add(purchase);
}
