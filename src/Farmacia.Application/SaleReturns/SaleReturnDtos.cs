using Farmacia.Domain.Sales;

namespace Farmacia.Application.SaleReturns;

/// <summary>The return voucher. No costs, like the ticket.</summary>
/// <param name="Refund">What the returned units were worth: CreditedToAccount + CashRefund.</param>
/// <param name="CreditedToAccount">The part that lowered the customer's credit debt.</param>
/// <param name="CashRefund">The cash given back.</param>
public sealed record SaleReturnDto(
    Guid Id,
    long Number,
    Guid SaleId,
    long SaleNumber,
    DateTimeOffset ReturnedAt,
    Guid CashSessionId,
    string ReturnedByName,
    string Reason,
    decimal Refund,
    decimal CreditedToAccount,
    decimal CashRefund,
    IReadOnlyList<SaleReturnItemDto> Items);

public sealed record SaleReturnItemDto(
    Guid Id,
    Guid SaleItemId,
    string ProductName,
    string SaleUnitName,
    int Quantity,
    decimal UnitPrice,
    decimal Refund,
    ReturnDisposition Disposition);
