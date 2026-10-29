using Farmacia.Domain.Sales;

namespace Farmacia.Application.SaleReturns;

/// <summary>The return voucher. No costs, like the ticket.</summary>
/// <param name="Refund">Cash given back.</param>
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
