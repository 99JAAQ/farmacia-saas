using Farmacia.Domain.Common;

namespace Farmacia.Domain.Sales;

/// <summary>One way the customer paid. Created only through <see cref="Sale"/>.</summary>
public class Payment : TenantEntity
{
    private Payment() { } // EF Core

    internal Payment(Guid pharmacyId, Guid saleId, PaymentMethod method, decimal amount, decimal? received)
        : base(pharmacyId)
    {
        SaleId = saleId;
        Method = method;
        Amount = amount;
        Received = received;
    }

    public Guid SaleId { get; private init; }
    public PaymentMethod Method { get; private init; }

    /// <summary>The part of the sale paid with this method. The payments of a sale add up to its total.</summary>
    public decimal Amount { get; private init; }

    /// <summary>Cash only: what the customer handed over. Null for the other methods.</summary>
    public decimal? Received { get; private init; }

    /// <summary>Cash only: the change given back.</summary>
    public decimal Change => (Received ?? Amount) - Amount;
}
