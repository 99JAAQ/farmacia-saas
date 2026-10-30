using Farmacia.Domain.Common;

namespace Farmacia.Domain.Credit;

/// <summary>
/// Aggregate root: a trusted customer's "fiado" account, like the notebook at the counter. Keeps the balance owed
/// and every change to it as a <see cref="CreditEntry"/> (the same pattern as a batch and its kardex), so the balance
/// can be checked against the limit under concurrency (RowVersion) and still be explained line by line.
/// </summary>
/// <remarks>The owner opens it and sets the limit; cashiers charge sales to it and receive payments.</remarks>
public class CreditAccount : TenantEntity
{
    private readonly List<CreditEntry> _pendingEntries = [];

    private CreditAccount() { } // EF Core

    private CreditAccount(Guid pharmacyId) : base(pharmacyId) { }

    public Guid CustomerId { get; private init; }

    /// <summary>The most the customer may owe. New sales on credit cannot go over it.</summary>
    public decimal Limit { get; private set; }

    /// <summary>What the customer owes now. Never negative: a payment cannot go over the debt.</summary>
    public decimal Balance { get; private set; }

    /// <summary>
    /// Since when the customer has owed without interruption: set when the balance goes from 0 to a debt, cleared
    /// when it is paid off. Tells the owner how old the debt is, without interest.
    /// </summary>
    public DateTimeOffset? DebtSince { get; private set; }

    /// <summary>A suspended account takes no new sales on credit, but still takes payments.</summary>
    public bool IsSuspended { get; private set; }

    public decimal Available => Math.Max(Limit - Balance, 0);

    /// <summary>Entries created since the account was loaded. Infrastructure saves them in the same transaction.</summary>
    public IReadOnlyList<CreditEntry> PendingEntries => _pendingEntries.AsReadOnly();

    public void ClearPendingEntries() => _pendingEntries.Clear();

    public static CreditAccount Open(Guid pharmacyId, Guid customerId, decimal limit)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("El cliente es obligatorio.");

        var account = new CreditAccount(pharmacyId) { CustomerId = customerId };
        account.ChangeLimit(limit);
        return account;
    }

    /// <summary>A limit below the current balance is allowed: it only stops new sales on credit until paid down.</summary>
    public void ChangeLimit(decimal limit)
    {
        if (limit <= 0)
            throw new DomainException("El cupo debe ser mayor que cero.");

        Limit = limit;
    }

    public void Suspend() => IsSuspended = true;

    public void Resume() => IsSuspended = false;

    /// <summary>Checks a sale on credit before anything changes (the sale validates everything first).</summary>
    public void EnsureCanCharge(decimal amount)
    {
        if (IsSuspended)
            throw new DomainException("El fiado de este cliente está suspendido.");

        if (amount <= 0)
            throw new DomainException("El valor fiado debe ser mayor que cero.");

        if (Balance + amount > Limit)
            throw new DomainException(
                $"El fiado supera el cupo del cliente: tiene disponibles {Money.Format(Available)} y se piden {Money.Format(amount)}.");
    }

    /// <summary>The part of a sale the customer takes on credit.</summary>
    public CreditEntry Charge(decimal amount, DateTimeOffset soldAt, Guid saleId)
    {
        EnsureCanCharge(amount);
        return Apply(CreditEntryType.Sale, amount, soldAt, saleId);
    }

    /// <summary>An "abono": it cannot go over the debt (there is no credit in the customer's favour).</summary>
    public CreditEntry ReceivePayment(decimal amount, DateTimeOffset receivedAt, Guid creditPaymentId)
    {
        if (amount <= 0)
            throw new DomainException("El abono debe ser mayor que cero.");

        if (amount > Balance)
            throw new DomainException(
                $"El abono ({Money.Format(amount)}) supera la deuda del cliente ({Money.Format(Balance)}).");

        return Apply(CreditEntryType.Payment, -amount, receivedAt, creditPaymentId);
    }

    /// <summary>A return of a sale taken on credit lowers the debt instead of giving cash back.</summary>
    public CreditEntry CreditReturn(decimal amount, DateTimeOffset returnedAt, Guid saleReturnId)
    {
        if (amount <= 0)
            throw new DomainException("El valor de la devolución debe ser mayor que cero.");

        if (amount > Balance)
            throw new DomainException(
                $"La devolución ({Money.Format(amount)}) supera la deuda del cliente ({Money.Format(Balance)}).");

        return Apply(CreditEntryType.Return, -amount, returnedAt, saleReturnId);
    }

    private CreditEntry Apply(CreditEntryType type, decimal change, DateTimeOffset occurredAt, Guid referenceId)
    {
        var wasClear = Balance == 0;
        Balance += change;

        if (wasClear && Balance > 0)
            DebtSince = occurredAt;
        else if (Balance == 0)
            DebtSince = null;

        var entry = new CreditEntry(this, type, change, occurredAt, referenceId);
        _pendingEntries.Add(entry);
        return entry;
    }
}

public enum CreditEntryType
{
    /// <summary>A sale taken on credit: the debt goes up.</summary>
    Sale = 1,

    /// <summary>An "abono": the debt goes down.</summary>
    Payment = 2,

    /// <summary>A return of something taken on credit: the debt goes down.</summary>
    Return = 3,
}

/// <summary>One change in a credit account's balance, like a kardex line. Created only by <see cref="CreditAccount"/>.</summary>
public class CreditEntry : TenantEntity
{
    private CreditEntry() { } // EF Core

    internal CreditEntry(CreditAccount account, CreditEntryType type, decimal change, DateTimeOffset occurredAt, Guid referenceId)
        : base(account.PharmacyId)
    {
        CreditAccountId = account.Id;
        CustomerId = account.CustomerId;
        Type = type;
        Change = change;
        BalanceAfter = account.Balance;
        OccurredAt = occurredAt;
        ReferenceId = referenceId;
    }

    public Guid CreditAccountId { get; private init; }
    public Guid CustomerId { get; private init; }
    public CreditEntryType Type { get; private init; }

    /// <summary>Positive when the debt goes up, negative when it goes down.</summary>
    public decimal Change { get; private init; }

    public decimal BalanceAfter { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>The sale, payment or return that caused it.</summary>
    public Guid ReferenceId { get; private init; }
}
