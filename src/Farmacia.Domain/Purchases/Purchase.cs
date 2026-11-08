using Farmacia.Domain.Common;
using Farmacia.Domain.Inventory;

namespace Farmacia.Domain.Purchases;

/// <summary>
/// Aggregate root: a supplier invoice. Typed in as a draft (it survives a power cut) and confirmed when the goods
/// are checked; confirming creates one <see cref="Batch"/> per line, with its real cost per base unit.
/// </summary>
public class Purchase : TenantEntity
{
    /// <summary>Accepted difference between the printed invoice total and the computed one (per-line rounding).</summary>
    public const decimal InvoiceTotalTolerance = 1m;

    private readonly List<PurchaseItem> _items = [];
    private readonly List<SupplierPayment> _payments = [];

    private Purchase() { } // EF Core

    private Purchase(Guid pharmacyId) : base(pharmacyId) { }

    public Guid SupplierId { get; private set; }

    /// <summary>The supplier's invoice number, upper-cased so "fe-12" and "FE-12" are the same invoice.</summary>
    public string InvoiceNumber { get; private set; } = null!;

    public DateOnly InvoiceDate { get; private set; }
    public PaymentTerm PaymentTerm { get; private set; }

    /// <summary>When a credit purchase must be paid. Always null for cash purchases.</summary>
    public DateOnly? DueDate { get; private set; }

    /// <summary>Discount on the whole invoice, spread over the lines in proportion to their net amount.</summary>
    public decimal InvoiceDiscount { get; private set; }

    public PurchaseStatus Status { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();

    /// <summary>Payments towards this invoice, if it was bought on credit.</summary>
    public IReadOnlyCollection<SupplierPayment> Payments => _payments.AsReadOnly();

    /// <summary>
    /// Only a confirmed credit invoice is owed: a cash purchase was paid when it was received, and a draft does not
    /// exist for the supplier yet.
    /// </summary>
    public bool IsPayable => Status == PurchaseStatus.Confirmed && PaymentTerm == PaymentTerm.Credit;

    /// <summary>
    /// Kept as a column, not summed from the payments: each payment then UPDATEs the purchase row, so its RowVersion
    /// stops two simultaneous payments from both passing the balance. Inserting only the payment row would not touch
    /// the purchase, and the concurrency check would never run.
    /// </summary>
    public decimal AmountPaid { get; private set; }

    /// <summary>
    /// What is still owed. The invoice's total is fixed once confirmed (it cannot be edited), so the balance is that
    /// total minus what was paid. Like any aggregate, it needs to be loaded whole (lines included), as its repository does.
    /// </summary>
    public decimal Balance => IsPayable ? CalculateTotals().Total - AmountPaid : 0;

    /// <summary>Unpaid after its due date, on the pharmacy's date.</summary>
    public bool IsOverdueOn(DateOnly today) => Balance > 0 && DueDate is { } due && today > due;

    public static Purchase CreateDraft(
        Guid pharmacyId, Guid supplierId, string invoiceNumber, DateOnly invoiceDate,
        PaymentTerm paymentTerm, DateOnly? dueDate = null)
    {
        var purchase = new Purchase(pharmacyId) { Status = PurchaseStatus.Draft };
        purchase.UpdateHeader(supplierId, invoiceNumber, invoiceDate, paymentTerm, dueDate);
        return purchase;
    }

    public void UpdateHeader(
        Guid supplierId, string invoiceNumber, DateOnly invoiceDate, PaymentTerm paymentTerm, DateOnly? dueDate)
    {
        EnsureDraft();

        if (supplierId == Guid.Empty)
            throw new DomainException("El proveedor es obligatorio.");

        if (!Enum.IsDefined(paymentTerm))
            throw new DomainException("La forma de pago no es válida.");

        if (paymentTerm == PaymentTerm.Credit)
        {
            if (dueDate is null)
                throw new DomainException("Una compra a crédito necesita la fecha de vencimiento del pago.");

            if (dueDate < invoiceDate)
                throw new DomainException("El vencimiento del pago no puede ser anterior a la fecha de la factura.");
        }

        SupplierId = supplierId;
        InvoiceNumber = Guard.Required(invoiceNumber, "El número de factura").ToUpperInvariant();
        InvoiceDate = invoiceDate;
        PaymentTerm = paymentTerm;
        DueDate = paymentTerm == PaymentTerm.Credit ? dueDate : null;
    }

    public void SetInvoiceDiscount(decimal discount)
    {
        EnsureDraft();

        if (discount < 0)
            throw new DomainException("El descuento de la factura no puede ser negativo.");

        InvoiceDiscount = discount;
    }

    public PurchaseItem AddItem(
        Guid productId, Guid saleUnitId, int unitsPerPackage, int quantity, int bonusQuantity,
        decimal unitCost, decimal lineDiscount, VatRate vatRate, string lotNumber, DateOnly expirationDate)
    {
        EnsureDraft();

        var item = new PurchaseItem(
            PharmacyId, Id, productId, saleUnitId, unitsPerPackage, quantity, bonusQuantity,
            unitCost, lineDiscount, vatRate, lotNumber, expirationDate);

        if (_items.Any(i => i.ProductId == item.ProductId
                            && string.Equals(i.LotNumber, item.LotNumber, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException(
                $"La factura ya tiene una línea de este producto con el lote {item.LotNumber}: corrija la cantidad de esa línea.");

        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureDraft();

        var item = _items.SingleOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("La línea no existe en esta compra.");

        _items.Remove(item);
    }

    /// <summary>
    /// A payment towards this credit invoice, in full or in part. It never goes over what is owed, and a transfer or a
    /// check carries its number so it can be matched with the bank statement. Paying is not editing: the lines, totals
    /// and batches stay as they were confirmed.
    /// </summary>
    /// <param name="paidAt">Local Colombian time.</param>
    public SupplierPayment RegisterPayment(
        decimal amount, SupplierPaymentMethod method, string? reference, DateTimeOffset paidAt, Guid paidBy)
    {
        if (!IsPayable)
            throw new DomainException(Status == PurchaseStatus.Draft
                ? "La compra todavía es un borrador: confírmela antes de pagarla."
                : "La compra fue de contado: no tiene saldo por pagar.");

        if (!Enum.IsDefined(method))
            throw new DomainException("El medio de pago no es válido.");

        if (amount <= 0)
            throw new DomainException("El pago debe ser mayor que cero.");

        var balance = Balance;
        if (amount > balance)
            throw new DomainException(
                $"El pago ({Money.Format(amount)}) supera el saldo de la factura {InvoiceNumber} ({Money.Format(balance)}).");

        var cleanReference = Guard.Optional(reference);
        if (method != SupplierPaymentMethod.Cash && cleanReference is null)
            throw new DomainException("Escriba el número de la transferencia o del cheque, para conciliarlo con el banco.");

        if (DateOnly.FromDateTime(paidAt.DateTime) < InvoiceDate)
            throw new DomainException("El pago no puede ser anterior a la fecha de la factura.");

        if (paidBy == Guid.Empty)
            throw new DomainException("El pago necesita quién lo registra.");

        var payment = new SupplierPayment(PharmacyId, Id, amount, method, cleanReference, paidAt, paidBy);
        _payments.Add(payment);
        AmountPaid += amount;
        return payment;
    }

    /// <summary>Totals as they would be on the invoice. Valid for drafts too (to show while typing).</summary>
    public PurchaseTotals CalculateTotals()
    {
        var lines = Breakdown();
        return new PurchaseTotals(
            Gross: _items.Sum(i => i.GrossAmount),
            LineDiscounts: _items.Sum(i => i.LineDiscount),
            InvoiceDiscount: InvoiceDiscount,
            TaxableBase: lines.Sum(l => l.TaxableBase),
            Vat: lines.Sum(l => l.Vat),
            Total: lines.Sum(l => l.TaxableBase + l.Vat));
    }

    /// <summary>
    /// Receives the goods: checks everything first and only then changes state, so a failure leaves the draft intact.
    /// Returns the new batches; the caller saves them in the same transaction as the purchase.
    /// </summary>
    /// <param name="printedInvoiceTotal">The total printed on the paper invoice, typed in to catch typing errors.</param>
    /// <param name="vatIsPartOfCost">False when the pharmacy is VAT-responsible: purchase VAT is recovered, not a cost.</param>
    /// <param name="receivedAt">Local Colombian time.</param>
    /// <param name="confirmedBy">Who received the goods: it goes into each batch's kardex entry.</param>
    public IReadOnlyList<Batch> Confirm(
        decimal printedInvoiceTotal, bool vatIsPartOfCost, DateTimeOffset receivedAt, Guid? confirmedBy = null)
    {
        EnsureDraft();

        if (_items.Count == 0)
            throw new DomainException("No se puede confirmar una compra sin líneas.");

        var receivedOn = DateOnly.FromDateTime(receivedAt.DateTime);
        if (InvoiceDate > receivedOn)
            throw new DomainException("La fecha de la factura no puede ser posterior a la fecha de recepción.");

        var netAmount = _items.Sum(i => i.NetAmount);
        if (InvoiceDiscount > netAmount)
            throw new DomainException(
                $"El descuento de la factura ({Money.Format(InvoiceDiscount)}) es mayor que el valor de las líneas ({Money.Format(netAmount)}).");

        var expired = _items.Where(i => i.ExpirationDate < receivedOn).Select(i => i.LotNumber).ToList();
        if (expired.Count > 0)
            throw new DomainException($"No se pueden recibir lotes vencidos: {string.Join(", ", expired)}.");

        var total = CalculateTotals().Total;
        var difference = printedInvoiceTotal - total;
        if (Math.Abs(difference) > InvoiceTotalTolerance)
            throw new DomainException(
                $"El total digitado ({Money.Format(printedInvoiceTotal)}) no coincide con el calculado ({Money.Format(total)}): " +
                $"diferencia de {Money.Format(Math.Abs(difference))}. Revise cantidades, costos y descuentos.");

        // Everything is valid: build the batches before touching this purchase.
        var received = Breakdown()
            .Select(line =>
            {
                var cost = line.TaxableBase + (vatIsPartOfCost ? line.Vat : 0);
                var unitCost = Math.Round(cost / line.Item.BaseUnits, 4, MidpointRounding.AwayFromZero);
                var batch = Batch.Receive(
                    PharmacyId, line.Item.ProductId, line.Item.LotNumber, line.Item.ExpirationDate,
                    line.Item.BaseUnits, unitCost, receivedAt, purchaseId: Id, receivedBy: confirmedBy);
                return (line.Item, Batch: batch, UnitCost: unitCost);
            })
            .ToList();

        foreach (var (item, batch, unitCost) in received)
            item.MarkReceived(batch.Id, unitCost);

        Status = PurchaseStatus.Confirmed;
        ConfirmedAt = receivedAt;

        return received.Select(r => r.Batch).ToList();
    }

    /// <summary>
    /// Spreads the invoice discount over the lines in proportion to their net amount (it lowers the VAT base).
    /// The last line takes the rounding remainder, so the shares always add up to the exact discount.
    /// </summary>
    private List<LineBreakdown> Breakdown()
    {
        var netAmount = _items.Sum(i => i.NetAmount);
        var lines = new List<LineBreakdown>(_items.Count);
        var allocated = 0m;

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var share = index == _items.Count - 1
                ? InvoiceDiscount - allocated
                : netAmount == 0 ? 0 : Money.Round(InvoiceDiscount * item.NetAmount / netAmount);
            allocated += share;

            var taxableBase = item.NetAmount - share;
            lines.Add(new LineBreakdown(item, taxableBase, Money.Round(taxableBase * item.VatRate.AsFraction())));
        }

        return lines;
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseStatus.Draft)
            throw new DomainException("La compra ya fue confirmada y no se puede modificar.");
    }

    private sealed record LineBreakdown(PurchaseItem Item, decimal TaxableBase, decimal Vat);
}

public sealed record PurchaseTotals(
    decimal Gross, decimal LineDiscounts, decimal InvoiceDiscount, decimal TaxableBase, decimal Vat, decimal Total);
