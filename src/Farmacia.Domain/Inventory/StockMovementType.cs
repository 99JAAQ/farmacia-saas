namespace Farmacia.Domain.Inventory;

public enum StockMovementType
{
    Purchase = 1,
    Sale = 2,
    Adjustment = 3,
    ExpiredWriteOff = 4,
}
