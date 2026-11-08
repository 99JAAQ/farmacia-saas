using Farmacia.Domain.Common;

namespace Farmacia.Domain.Purchases;

/// <summary>A payment towards one credit invoice. Created only through <see cref="Purchase.RegisterPayment"/>.</summary>
public class SupplierPayment : TenantEntity
{
    private SupplierPayment() { } // EF Core

    internal SupplierPayment(
        Guid pharmacyId, Guid purchaseId, decimal amount, SupplierPaymentMethod method, string? reference,
        DateTimeOffset paidAt, Guid paidBy)
        : base(pharmacyId)
    {
        PurchaseId = purchaseId;
        Amount = amount;
        Method = method;
        Reference = reference;
        PaidAt = paidAt;
        PaidBy = paidBy;
    }

    public Guid PurchaseId { get; private init; }
    public decimal Amount { get; private init; }
    public SupplierPaymentMethod Method { get; private init; }

    /// <summary>The transfer or check number, to match it with the bank statement.</summary>
    public string? Reference { get; private init; }

    public DateTimeOffset PaidAt { get; private init; }
    public Guid PaidBy { get; private init; }
}
