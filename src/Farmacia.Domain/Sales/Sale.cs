using Farmacia.Domain.Common;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Products;

namespace Farmacia.Domain.Sales;

/// <summary>A product line as the cashier rings it up, with the batches of that product to sell from.</summary>
/// <param name="Batches">All the product's batches with stock; FEFO picks from them.</param>
public sealed record SaleLine(Product Product, Guid SaleUnitId, int Quantity, IReadOnlyList<Batch> Batches);

/// <summary>What the customer handed over with one method. For cash, the amount received (change is computed).</summary>
public sealed record PaymentLine(PaymentMethod Method, decimal Amount);

/// <summary>
/// Aggregate root: a completed sale (a ticket). The cart lives in the browser; the sale arrives complete and is
/// registered in one step: lines priced from the catalog, stock taken by FEFO, payments checked against the total.
/// </summary>
public class Sale : TenantEntity
{
    private readonly List<SaleItem> _items = [];
    private readonly List<Payment> _payments = [];

    private Sale() { } // EF Core

    private Sale(Guid pharmacyId) : base(pharmacyId) { }

    /// <summary>Ticket number, consecutive per pharmacy. Assigned by the use case.</summary>
    public long Number { get; private init; }

    public Guid CashSessionId { get; private init; }

    /// <summary>The customer, when the sale has one (always when part of it is on credit).</summary>
    public Guid? CustomerId { get; private init; }

    public Guid SoldBy { get; private init; }
    public DateTimeOffset SoldAt { get; private init; }

    /// <summary>Who confirmed they checked the prescription, when the sale had products that need one.</summary>
    public Guid? PrescriptionCheckedBy { get; private init; }

    public DateTimeOffset? PrescriptionCheckedAt { get; private init; }

    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    /// <summary>VAT included.</summary>
    public decimal Total => _items.Sum(i => i.Total);

    public decimal Vat => _items.Sum(i => i.Vat);

    public decimal Change => _payments.Sum(p => p.Change);

    /// <summary>The part taken on credit ("fiado"), charged to the customer's account.</summary>
    public decimal CreditAmount => _payments.Where(p => p.Method == PaymentMethod.Credit).Sum(p => p.Amount);

    /// <summary>
    /// Registers the sale. Checks everything first (stock of every product, prescription, payments) and only then
    /// takes the stock, so a failure leaves every batch untouched. The caller saves the sale and the batches
    /// (with their kardex movements) in one transaction.
    /// </summary>
    /// <param name="prescriptionChecked">The cashier ticked "Fórmula revisada".</param>
    /// <param name="soldAt">Local Colombian time.</param>
    /// <param name="creditAccount">The customer's account, required when part of the sale is on credit.</param>
    public static Sale Register(
        Guid pharmacyId, long number, Guid cashSessionId, Guid soldBy, DateTimeOffset soldAt,
        IReadOnlyList<SaleLine> lines, IReadOnlyList<PaymentLine> payments, bool prescriptionChecked,
        CreditAccount? creditAccount = null)
    {
        if (creditAccount is not null && creditAccount.PharmacyId != pharmacyId)
            throw new ArgumentException("The credit account must be of the same pharmacy.", nameof(creditAccount));

        if (number <= 0)
            throw new DomainException("El número del tiquete debe ser mayor que cero.");

        if (cashSessionId == Guid.Empty || soldBy == Guid.Empty)
            throw new DomainException("La venta necesita el turno de caja y el cajero.");

        if (lines.Count == 0)
            throw new DomainException("La venta no tiene productos.");

        var needsPrescription = lines.Any(l => l.Product.RequiresPrescription);
        if (needsPrescription && !prescriptionChecked)
            throw new DomainException("La venta tiene productos que requieren fórmula médica: confirme que la revisó.");

        var sale = new Sale(pharmacyId)
        {
            Number = number,
            CashSessionId = cashSessionId,
            SoldBy = soldBy,
            SoldAt = soldAt,
            CustomerId = creditAccount?.CustomerId,
            PrescriptionCheckedBy = needsPrescription ? soldBy : null,
            PrescriptionCheckedAt = needsPrescription ? soldAt : null,
        };

        var priced = lines.Select(sale.Price).ToList();
        EnsureStock(priced, DateOf(soldAt));
        sale.AddPayments(payments, creditAccount);

        // Everything is valid: now take the stock, line by line (lines of the same product see what the previous took).
        foreach (var (line, item) in priced)
            foreach (var allocation in FefoAllocator.Allocate(line.Batches, item.BaseUnits, DateOf(soldAt)))
            {
                allocation.Batch.Sell(allocation.Quantity, soldAt, sale.Id, soldBy);
                item.TakeFrom(allocation.Batch.Id, allocation.Quantity, allocation.Batch.UnitCost);
            }

        // The limit was checked with the payments; the debt goes up only now, with everything else valid.
        if (sale.CreditAmount > 0)
            creditAccount!.Charge(sale.CreditAmount, soldAt, sale.Id);

        return sale;
    }

    private (SaleLine Line, SaleItem Item) Price(SaleLine line)
    {
        if (!line.Product.IsActive)
            throw new DomainException($"{line.Product.Name} está inactivo: ya no se vende.");

        if (line.Quantity <= 0)
            throw new DomainException($"La cantidad de {line.Product.Name} debe ser mayor que cero.");

        var unit = line.Product.SaleUnits.SingleOrDefault(u => u.Id == line.SaleUnitId)
            ?? throw new DomainException($"La presentación indicada no es de {line.Product.Name}.");

        var item = new SaleItem(
            PharmacyId, Id, line.Product.Id, unit.Id, line.Quantity, unit.Price, line.Product.VatRate,
            unit.ConversionFactor, line.Product.RequiresPrescription);
        _items.Add(item);
        return (line, item);
    }

    /// <summary>
    /// Several lines may be the same product (a box and loose tablets): they draw from the same batches, so the
    /// stock is checked for their sum. FefoAllocator throws if it is not enough.
    /// </summary>
    private static void EnsureStock(List<(SaleLine Line, SaleItem Item)> priced, DateOnly date)
    {
        foreach (var product in priced.GroupBy(p => p.Line.Product.Id))
        {
            var units = product.Sum(p => p.Item.BaseUnits);
            var name = product.First().Line.Product.Name;
            try
            {
                FefoAllocator.Allocate(product.First().Line.Batches, units, date);
            }
            catch (DomainException ex)
            {
                throw new DomainException($"{name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Card, wallet, transfer and credit pay their exact amount and cannot go over the total (nobody gives change on
    /// a transfer). Cash covers the rest: what the customer hands over minus that rest is the change. Credit needs the
    /// customer's account, and is checked against its limit here, before any stock is taken.
    /// </summary>
    private void AddPayments(IReadOnlyList<PaymentLine> payments, CreditAccount? creditAccount)
    {
        if (payments.Any(p => !Enum.IsDefined(p.Method)))
            throw new DomainException("El medio de pago no es válido.");

        if (payments.Any(p => p.Amount <= 0))
            throw new DomainException("Cada pago debe ser mayor que cero.");

        var cash = payments.Where(p => p.Method == PaymentMethod.Cash).ToList();
        if (cash.Count > 1)
            throw new DomainException("Registre el efectivo en un solo pago.");

        var credit = payments.Where(p => p.Method == PaymentMethod.Credit).ToList();
        if (credit.Count > 1)
            throw new DomainException("Registre el fiado en un solo pago.");

        if (credit.Count == 1)
        {
            // Without the account, a "credit" payment would be a free sale.
            if (creditAccount is null)
                throw new DomainException("Para fiar, elija un cliente con cupo de fiado.");

            creditAccount.EnsureCanCharge(credit[0].Amount);
        }

        var total = Total;
        var nonCash = payments.Where(p => p.Method != PaymentMethod.Cash).ToList();
        var nonCashTotal = nonCash.Sum(p => p.Amount);

        if (nonCashTotal > total)
            throw new DomainException(
                $"Los pagos que no son en efectivo ({Money.Format(nonCashTotal)}) superan el total ({Money.Format(total)}).");

        var cashDue = total - nonCashTotal;

        if (cash.Count == 0 && cashDue > 0)
            throw new DomainException($"Falta pagar {Money.Format(cashDue)}.");

        if (cash.Count == 1)
        {
            if (cashDue == 0)
                throw new DomainException("Los otros pagos ya cubren el total: no hace falta efectivo.");

            if (cash[0].Amount < cashDue)
                throw new DomainException(
                    $"El efectivo recibido ({Money.Format(cash[0].Amount)}) no alcanza: faltan {Money.Format(cashDue - cash[0].Amount)}.");

            _payments.Add(new Payment(PharmacyId, Id, PaymentMethod.Cash, cashDue, received: cash[0].Amount));
        }

        foreach (var payment in nonCash)
            _payments.Add(new Payment(PharmacyId, Id, payment.Method, payment.Amount, received: null));
    }

    private static DateOnly DateOf(DateTimeOffset timestamp) => DateOnly.FromDateTime(timestamp.DateTime);
}
