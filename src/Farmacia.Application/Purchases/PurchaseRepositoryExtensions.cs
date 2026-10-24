using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Purchases;

internal static class PurchaseRepositoryExtensions
{
    public static async Task<Purchase> GetOrThrowAsync(
        this IPurchaseRepository purchases, Guid pharmacyId, Guid purchaseId, CancellationToken cancellationToken) =>
        await purchases.GetByIdAsync(pharmacyId, purchaseId, cancellationToken)
            ?? throw new NotFoundException("La compra no existe.");

    /// <summary>
    /// Call after the domain set the invoice number, so the check uses the normalized value ("fe-12" = "FE-12").
    /// The unique index is the final guard against two simultaneous requests.
    /// </summary>
    public static async Task EnsureInvoiceIsNotRegisteredAsync(
        this IPurchaseRepository purchases, Purchase purchase, CancellationToken cancellationToken)
    {
        if (await purchases.InvoiceExistsAsync(
                purchase.PharmacyId, purchase.SupplierId, purchase.InvoiceNumber, purchase.Id, cancellationToken))
            throw new ConflictException($"La factura {purchase.InvoiceNumber} de este proveedor ya está registrada.");
    }

    public static async Task EnsureSupplierExistsAsync(
        this ISupplierRepository suppliers, Guid pharmacyId, Guid supplierId, CancellationToken cancellationToken)
    {
        if (!await suppliers.ExistsAsync(pharmacyId, supplierId, cancellationToken))
            throw new ValidationException("El proveedor indicado no existe.");
    }
}
