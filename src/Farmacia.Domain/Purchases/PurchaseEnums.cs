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
