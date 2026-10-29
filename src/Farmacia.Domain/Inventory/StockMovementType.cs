namespace Farmacia.Domain.Inventory;

public enum StockMovementType
{
    Purchase = 1,
    Sale = 2,
    Adjustment = 3,
    ExpiredWriteOff = 4,

    /// <summary>Units a customer brought back, put back into the batch they were sold from.</summary>
    CustomerReturn = 5,
}
