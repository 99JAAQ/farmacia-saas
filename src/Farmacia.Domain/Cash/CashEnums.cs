namespace Farmacia.Domain.Cash;

public enum CashSessionStatus
{
    /// <summary>The cashier is working: sales and cash movements go to this shift.</summary>
    Open = 1,

    /// <summary>The cash was counted. Can no longer be changed.</summary>
    Closed = 2,
}

public enum CashMovementType
{
    /// <summary>Cash that comes in and is not a sale (e.g. change brought from the bank).</summary>
    Income = 1,

    /// <summary>Cash that goes out and is not a refund (a delivery, an owner withdrawal, a small supplier payment).</summary>
    Expense = 2,
}
