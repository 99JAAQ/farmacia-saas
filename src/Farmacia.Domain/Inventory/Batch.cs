using Farmacia.Domain.Common;

namespace Farmacia.Domain.Inventory;

/// <summary>
/// Aggregate root. A lot of a product with its own expiration date and cost; quantities are in base units.
/// Every quantity change produces a <see cref="StockMovement"/>.
/// </summary>
/// <remarks>
/// Dates are taken from the timestamps passed in, which must carry the pharmacy's local offset (Colombia, UTC-5):
/// a UTC timestamp at 8 p.m. would already be the next day and could expire a batch one day early.
/// </remarks>
public class Batch : TenantEntity
{
    private readonly List<StockMovement> _pendingMovements = [];

    private Batch() { } // EF Core

    private Batch(Guid pharmacyId) : base(pharmacyId) { }

    public Guid ProductId { get; private init; }
    public string LotNumber { get; private init; } = null!;
    public DateOnly ExpirationDate { get; private init; }

    /// <summary>Cost per base unit. Zero is allowed for supplier bonuses.</summary>
    public decimal UnitCost { get; private init; }

    public DateTimeOffset ReceivedAt { get; private init; }

    /// <summary>Never negative while online. Offline sync (phase 6) may break this and flag it for review.</summary>
    public int Quantity { get; private set; }

    /// <summary>Movements created since the batch was loaded. Infrastructure saves them in the same transaction.</summary>
    public IReadOnlyList<StockMovement> PendingMovements => _pendingMovements.AsReadOnly();

    public void ClearPendingMovements() => _pendingMovements.Clear();

    /// <summary>A batch can be sold through its expiration date; it is expired from the next day on.</summary>
    public bool IsExpiredOn(DateOnly date) => date > ExpirationDate;

    public static Batch Receive(
        Guid pharmacyId, Guid productId, string lotNumber, DateOnly expirationDate,
        int quantity, decimal unitCost, DateTimeOffset receivedAt, Guid? purchaseId = null)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El producto es obligatorio.");

        if (quantity <= 0)
            throw new DomainException("La cantidad recibida debe ser mayor que cero.");

        if (unitCost < 0)
            throw new DomainException("El costo no puede ser negativo.");

        var batch = new Batch(pharmacyId)
        {
            ProductId = productId,
            LotNumber = Guard.Required(lotNumber, "El número de lote"),
            ExpirationDate = expirationDate,
            UnitCost = unitCost,
            ReceivedAt = receivedAt,
        };

        if (batch.IsExpiredOn(DateOf(receivedAt)))
            throw new DomainException($"No se puede recibir el lote {batch.LotNumber}: está vencido.");

        batch.ApplyChange(StockMovementType.Purchase, quantity, receivedAt, purchaseId, notes: null);
        return batch;
    }

    public StockMovement Sell(int quantity, DateTimeOffset soldAt, Guid saleId)
    {
        if (IsExpiredOn(DateOf(soldAt)))
            throw new DomainException($"El lote {LotNumber} está vencido y no se puede vender.");

        if (quantity <= 0)
            throw new DomainException("La cantidad vendida debe ser mayor que cero.");

        if (quantity > Quantity)
            throw new DomainException(
                $"Stock insuficiente en el lote {LotNumber}: disponible {Quantity}, solicitado {quantity}.");

        return ApplyChange(StockMovementType.Sale, -quantity, soldAt, saleId, notes: null);
    }

    /// <summary>
    /// Units a customer returned go back into this batch. An expired batch does not take them back: they could not be
    /// sold again, so the return must discard them.
    /// </summary>
    public StockMovement Restock(int quantity, DateTimeOffset returnedAt, Guid saleReturnId)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad devuelta debe ser mayor que cero.");

        if (IsExpiredOn(DateOf(returnedAt)))
            throw new DomainException($"El lote {LotNumber} está vencido: las unidades devueltas no pueden volver al inventario.");

        return ApplyChange(StockMovementType.CustomerReturn, quantity, returnedAt, saleReturnId, notes: null);
    }

    public StockMovement WriteOffExpired(DateTimeOffset writtenOffAt)
    {
        if (!IsExpiredOn(DateOf(writtenOffAt)))
            throw new DomainException($"El lote {LotNumber} no está vencido.");

        if (Quantity == 0)
            throw new DomainException($"El lote {LotNumber} no tiene unidades para dar de baja.");

        return ApplyChange(StockMovementType.ExpiredWriteOff, -Quantity, writtenOffAt, referenceId: null, notes: null);
    }

    /// <summary>Sets the quantity to what was physically counted, recording the difference.</summary>
    public StockMovement AdjustToCount(int countedQuantity, string reason, DateTimeOffset adjustedAt)
    {
        if (countedQuantity < 0)
            throw new DomainException("La cantidad contada no puede ser negativa.");

        var notes = Guard.Required(reason, "El motivo del ajuste");
        var change = countedQuantity - Quantity;

        if (change == 0)
            throw new DomainException($"El conteo coincide con el stock del lote {LotNumber}; no hay nada que ajustar.");

        return ApplyChange(StockMovementType.Adjustment, change, adjustedAt, referenceId: null, notes);
    }

    private StockMovement ApplyChange(
        StockMovementType type, int change, DateTimeOffset occurredAt, Guid? referenceId, string? notes)
    {
        Quantity += change;

        var movement = new StockMovement(this, type, change, occurredAt, referenceId, notes);
        _pendingMovements.Add(movement);
        return movement;
    }

    private static DateOnly DateOf(DateTimeOffset timestamp) => DateOnly.FromDateTime(timestamp.DateTime);
}
