using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Inventory;

namespace Farmacia.Domain.Sales;

/// <summary>What happens to the returned units.</summary>
public enum ReturnDisposition
{
    /// <summary>Back into the batch they were sold from, to be sold again.</summary>
    Restock = 1,

    /// <summary>Opened, damaged or out of the cold chain: they do not go back to stock.</summary>
    Discard = 2,
}

/// <summary>One line of the ticket being returned, in its sale unit (e.g. 1 of the 3 boxes).</summary>
public sealed record ReturnLine(Guid SaleItemId, int Quantity, ReturnDisposition Disposition);

/// <summary>
/// Aggregate root: a customer brings back part or all of a sale and gets the money back. The sale itself is never
/// changed (a ticket is a closed document): what is left to return is the sale minus its earlier returns.
/// If part of the sale was on credit, the refund first lowers the customer's debt (ADR 0010); the rest is cash, out
/// of the shift of whoever makes the return (the sale's shift is usually closed by then).
/// </summary>
public class SaleReturn : TenantEntity
{
    private readonly List<SaleReturnItem> _items = [];

    private SaleReturn() { } // EF Core

    private SaleReturn(Guid pharmacyId) : base(pharmacyId) { }

    /// <summary>Consecutive per pharmacy, separate from the tickets. Assigned by the use case.</summary>
    public long Number { get; private init; }

    public Guid SaleId { get; private init; }
    public Guid CashSessionId { get; private init; }
    public Guid ReturnedBy { get; private init; }
    public DateTimeOffset ReturnedAt { get; private init; }
    public string Reason { get; private init; } = null!;

    public IReadOnlyCollection<SaleReturnItem> Items => _items.AsReadOnly();

    /// <summary>What the returned units were worth (VAT included): the debt lowered plus the cash given back.</summary>
    public decimal Refund => _items.Sum(i => i.Refund);

    /// <summary>The part of the refund that lowered the customer's credit debt instead of being paid in cash.</summary>
    public decimal CreditedToAccount { get; private set; }

    /// <summary>The cash that leaves the drawer.</summary>
    public decimal CashRefund => Refund - CreditedToAccount;

    /// <summary>
    /// Registers the return. Checks every line first (it belongs to the ticket, it was not returned already, its batch
    /// can take the units back) and only then restocks, so a failure leaves every batch untouched. The caller saves
    /// the return and the batches (with their kardex movements) in one transaction.
    /// </summary>
    /// <param name="previousReturns">The sale's earlier returns: together they cannot return more than was sold.</param>
    /// <param name="batches">The batches the returned lines were sold from.</param>
    /// <param name="returnedAt">Local Colombian time.</param>
    /// <param name="creditAccount">The customer's account, required when part of the sale was on credit.</param>
    public static SaleReturn Register(
        long number, Sale sale, IReadOnlyList<SaleReturn> previousReturns, Guid cashSessionId, Guid returnedBy,
        DateTimeOffset returnedAt, string reason, IReadOnlyList<ReturnLine> lines, IReadOnlyList<Batch> batches,
        CreditAccount? creditAccount = null)
    {
        if (previousReturns.Any(r => r.SaleId != sale.Id))
            throw new ArgumentException("The previous returns must be of the same sale.", nameof(previousReturns));

        if (sale.CreditAmount > 0 && (creditAccount is null || creditAccount.CustomerId != sale.CustomerId))
            throw new ArgumentException("A sale with credit needs its customer's account to be returned.", nameof(creditAccount));

        if (number <= 0)
            throw new DomainException("El número de la devolución debe ser mayor que cero.");

        if (cashSessionId == Guid.Empty || returnedBy == Guid.Empty)
            throw new DomainException("La devolución necesita el turno de caja y el cajero.");

        if (returnedAt < sale.SoldAt)
            throw new DomainException("La devolución no puede ser anterior a la venta.");

        if (lines.Count == 0)
            throw new DomainException("La devolución no tiene productos.");

        if (lines.Select(l => l.SaleItemId).Distinct().Count() != lines.Count)
            throw new DomainException("Cada línea del tiquete va una sola vez en la devolución.");

        var saleReturn = new SaleReturn(sale.PharmacyId)
        {
            Number = number,
            SaleId = sale.Id,
            CashSessionId = cashSessionId,
            ReturnedBy = returnedBy,
            ReturnedAt = returnedAt,
            Reason = Guard.Required(reason, "El motivo de la devolución"),
        };

        var earlierItems = previousReturns.SelectMany(r => r.Items).ToList();
        var date = DateOnly.FromDateTime(returnedAt.DateTime);
        var restocks = new List<(SaleReturnItem Item, Batch Batch, int Quantity)>();

        foreach (var line in lines)
        {
            var sold = sale.Items.SingleOrDefault(i => i.Id == line.SaleItemId)
                ?? throw new DomainException("Una línea de la devolución no es de este tiquete.");

            if (!Enum.IsDefined(line.Disposition))
                throw new DomainException("Indique si las unidades devueltas vuelven al inventario o se descartan.");

            if (line.Quantity <= 0)
                throw new DomainException("La cantidad devuelta debe ser mayor que cero.");

            var returnable = sold.Quantity - earlierItems.Where(i => i.SaleItemId == sold.Id).Sum(i => i.Quantity);
            if (line.Quantity > returnable)
                throw new DomainException(returnable == 0
                    ? "Esa línea del tiquete ya se devolvió completa."
                    : $"De esa línea del tiquete solo quedan {returnable} por devolver.");

            var item = new SaleReturnItem(saleReturn.PharmacyId, saleReturn.Id, sold, line.Quantity, line.Disposition);
            saleReturn._items.Add(item);

            if (line.Disposition == ReturnDisposition.Restock)
                restocks.AddRange(PlanRestock(sold, item, earlierItems, batches, date));
        }

        // Everything is valid: now the units go back into their batches.
        foreach (var (item, batch, quantity) in restocks)
        {
            batch.Restock(quantity, returnedAt, saleReturn.Id, returnedBy);
            item.PutBackInto(batch.Id, quantity, batch.UnitCost);
        }

        // What was taken on credit is not given back in cash: it lowers the debt, up to what this ticket put on credit
        // (minus what earlier returns already lowered) and never below zero. If the customer already paid it off with
        // "abonos", that money was really paid, so it comes back in cash.
        var creditLeft = sale.CreditAmount - previousReturns.Sum(r => r.CreditedToAccount);
        var credited = Math.Min(saleReturn.Refund, Math.Min(creditLeft, creditAccount?.Balance ?? 0));
        if (credited > 0)
        {
            creditAccount!.CreditReturn(credited, returnedAt, saleReturn.Id);
            saleReturn.CreditedToAccount = credited;
        }

        return saleReturn;
    }

    /// <summary>
    /// Which batches take the units back. A line may have come from several batches (FEFO); the units go back to the
    /// latest-expiring first, the reverse of FEFO, so returning everything leaves the batches as before the sale.
    /// Each batch takes at most what the line took from it, minus what earlier returns already put back.
    /// </summary>
    private static IEnumerable<(SaleReturnItem, Batch, int)> PlanRestock(
        SaleItem sold, SaleReturnItem item, List<SaleReturnItem> earlierItems, IReadOnlyList<Batch> batches, DateOnly date)
    {
        var putBackBefore = earlierItems
            .Where(i => i.SaleItemId == sold.Id)
            .SelectMany(i => i.Batches)
            .GroupBy(b => b.BatchId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));

        var sources = sold.Batches
            .Select(taken => (
                Batch: batches.SingleOrDefault(b => b.Id == taken.BatchId)
                    ?? throw new DomainException("Falta un lote del que salió la venta."),
                Room: taken.Quantity - putBackBefore.GetValueOrDefault(taken.BatchId)))
            .Where(source => source.Room > 0)
            .OrderByDescending(source => source.Batch.ExpirationDate)
            .ThenBy(source => source.Batch.LotNumber);

        var pending = item.BaseUnits;
        var plan = new List<(SaleReturnItem, Batch, int)>();
        foreach (var (batch, room) in sources)
        {
            if (pending == 0) break;

            if (batch.IsExpiredOn(date))
                throw new DomainException(
                    $"El lote {batch.LotNumber} está vencido: las unidades devueltas no pueden volver al inventario, descártelas.");

            var quantity = Math.Min(room, pending);
            plan.Add((item, batch, quantity));
            pending -= quantity;
        }

        // Cannot happen while the quantity check above holds; guards the invariant if that check ever changes.
        if (pending > 0)
            throw new InvalidOperationException("The batches of the sale cannot take back the returned units.");

        return plan;
    }
}

/// <summary>A returned part of one ticket line. Price and factor are the sale's, so the refund is what was paid.</summary>
public class SaleReturnItem : TenantEntity
{
    private readonly List<SaleReturnItemBatch> _batches = [];

    private SaleReturnItem() { } // EF Core

    internal SaleReturnItem(Guid pharmacyId, Guid saleReturnId, SaleItem sold, int quantity, ReturnDisposition disposition)
        : base(pharmacyId)
    {
        SaleReturnId = saleReturnId;
        SaleItemId = sold.Id;
        ProductId = sold.ProductId;
        SaleUnitId = sold.SaleUnitId;
        Quantity = quantity;
        UnitPrice = sold.UnitPrice;
        ConversionFactor = sold.ConversionFactor;
        Disposition = disposition;
    }

    public Guid SaleReturnId { get; private init; }
    public Guid SaleItemId { get; private init; }
    public Guid ProductId { get; private init; }
    public Guid SaleUnitId { get; private init; }

    /// <summary>In sale units, as sold (e.g. 1 box).</summary>
    public int Quantity { get; private init; }

    /// <summary>The price paid per sale unit, VAT included.</summary>
    public decimal UnitPrice { get; private init; }

    public int ConversionFactor { get; private init; }
    public ReturnDisposition Disposition { get; private init; }

    /// <summary>Restocked units only: the batches they went back to, with their cost.</summary>
    public IReadOnlyCollection<SaleReturnItemBatch> Batches => _batches.AsReadOnly();

    public int BaseUnits => Quantity * ConversionFactor;

    public decimal Refund => Quantity * UnitPrice;

    internal void PutBackInto(Guid batchId, int quantity, decimal unitCost) =>
        _batches.Add(new SaleReturnItemBatch(PharmacyId, Id, batchId, quantity, unitCost));
}

/// <summary>How many base units of a <see cref="SaleReturnItem"/> went back into one batch.</summary>
public class SaleReturnItemBatch : TenantEntity
{
    private SaleReturnItemBatch() { } // EF Core

    internal SaleReturnItemBatch(Guid pharmacyId, Guid saleReturnItemId, Guid batchId, int quantity, decimal unitCost)
        : base(pharmacyId)
    {
        SaleReturnItemId = saleReturnItemId;
        BatchId = batchId;
        Quantity = quantity;
        UnitCost = unitCost;
    }

    public Guid SaleReturnItemId { get; private init; }
    public Guid BatchId { get; private init; }

    /// <summary>In base units.</summary>
    public int Quantity { get; private init; }

    /// <summary>The batch's cost per base unit.</summary>
    public decimal UnitCost { get; private init; }
}
