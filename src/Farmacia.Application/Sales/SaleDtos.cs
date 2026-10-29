using Farmacia.Domain.Common;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Sales;

/// <summary>
/// The ticket. No costs: the cashier sees it, and margins stay out of the cashier's view (as with purchases).
/// </summary>
public sealed record SaleDto(
    Guid Id,
    long Number,
    DateTimeOffset SoldAt,
    Guid CashSessionId,
    string SoldByName,
    decimal Total,
    decimal Vat,
    decimal Change,
    bool PrescriptionChecked,
    IReadOnlyList<SaleItemDto> Items,
    IReadOnlyList<PaymentDto> Payments);

public sealed record SaleItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string SaleUnitName,
    int Quantity,
    decimal UnitPrice,
    VatRate VatRate,
    decimal Total,
    decimal Vat,
    bool RequiresPrescription);

/// <param name="Amount">The part of the sale paid with this method.</param>
/// <param name="Received">Cash only: what the customer handed over.</param>
public sealed record PaymentDto(PaymentMethod Method, decimal Amount, decimal? Received, decimal Change);
