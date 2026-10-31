using Farmacia.Domain.Sales;

namespace Farmacia.Application.Credit;

/// <summary>The payment voucher ("abono"): what was paid and what is still owed.</summary>
public sealed record CreditPaymentDto(
    Guid Id,
    long Number,
    Guid CustomerId,
    string CustomerName,
    DateTimeOffset ReceivedAt,
    Guid CashSessionId,
    string ReceivedByName,
    PaymentMethod Method,
    decimal Amount,
    decimal BalanceAfter);
