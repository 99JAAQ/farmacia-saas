namespace Farmacia.Domain.Purchases;

public enum PurchaseStatus
{
    /// <summary>Being typed in: can be edited, does not touch the inventory.</summary>
    Draft = 1,

    /// <summary>Received: its batches are in the inventory. Can no longer be edited.</summary>
    Confirmed = 2,
}

public enum PaymentTerm
{
    /// <summary>Paid on delivery.</summary>
    Cash = 1,

    /// <summary>Paid later: needs a due date.</summary>
    Credit = 2,
}

/// <summary>How a supplier invoice is paid. Always outside the register's drawer (ADR 0013).</summary>
public enum SupplierPaymentMethod
{
    BankTransfer = 1,

    /// <summary>From the safe, not from the register's drawer.</summary>
    Cash = 2,

    Check = 3,
}
