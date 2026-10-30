using Farmacia.Domain.Common;
using Farmacia.Domain.Sales;

namespace Farmacia.Domain.Credit;

/// <summary>
/// Aggregate root: an "abono", a payment towards a customer's credit balance (not towards a specific ticket, like the
/// notebook at the counter). It has its own consecutive number and voucher. In cash, it goes into the drawer of
/// whoever receives it.
/// </summary>
public class CreditPayment : TenantEntity
{
    private CreditPayment() { } // EF Core

    private CreditPayment(Guid pharmacyId) : base(pharmacyId) { }

    /// <summary>Consecutive per pharmacy, separate from tickets and returns. Assigned by the use case.</summary>
    public long Number { get; private init; }

    public Guid CreditAccountId { get; private init; }
    public Guid CustomerId { get; private init; }
    public Guid CashSessionId { get; private init; }
    public Guid ReceivedBy { get; private init; }
    public DateTimeOffset ReceivedAt { get; private init; }

    /// <summary>Cash, card, wallet or transfer; never credit (a debt is not paid with more debt).</summary>
    public PaymentMethod Method { get; private init; }

    public decimal Amount { get; private init; }

    /// <summary>What the customer still owes right after this payment, for the voucher.</summary>
    public decimal BalanceAfter { get; private set; }

    /// <summary>Registers the payment and lowers the account's balance; the caller saves both in one transaction.</summary>
    /// <param name="receivedAt">Local Colombian time.</param>
    public static CreditPayment Register(
        long number, CreditAccount account, Guid cashSessionId, Guid receivedBy, DateTimeOffset receivedAt,
        PaymentMethod method, decimal amount)
    {
        if (number <= 0)
            throw new DomainException("El número del abono debe ser mayor que cero.");

        if (cashSessionId == Guid.Empty || receivedBy == Guid.Empty)
            throw new DomainException("El abono necesita el turno de caja y el cajero.");

        if (!Enum.IsDefined(method) || method == PaymentMethod.Credit)
            throw new DomainException("El abono se paga en efectivo, con tarjeta, con billetera o por transferencia.");

        var payment = new CreditPayment(account.PharmacyId)
        {
            Number = number,
            CreditAccountId = account.Id,
            CustomerId = account.CustomerId,
            CashSessionId = cashSessionId,
            ReceivedBy = receivedBy,
            ReceivedAt = receivedAt,
            Method = method,
            Amount = amount,
        };

        // The account checks the amount (positive, not over the debt); only then is the voucher complete.
        payment.BalanceAfter = account.ReceivePayment(amount, receivedAt, payment.Id).BalanceAfter;
        return payment;
    }
}
