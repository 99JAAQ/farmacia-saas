using Farmacia.Domain.Common;
using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Purchases;

/// <param name="AmountPaid">Paid to the supplier so far (credit invoices).</param>
/// <param name="Balance">Still owed: only a confirmed credit invoice owes anything.</param>
/// <param name="IsOverdue">Unpaid after its due date, on the pharmacy's date.</param>
public sealed record PurchaseDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    PaymentTerm PaymentTerm,
    DateOnly? DueDate,
    decimal InvoiceDiscount,
    PurchaseStatus Status,
    DateTimeOffset? ConfirmedAt,
    PurchaseTotals Totals,
    IReadOnlyList<PurchaseItemDto> Items,
    decimal AmountPaid,
    decimal Balance,
    bool IsOverdue,
    IReadOnlyList<SupplierPaymentDto> Payments);

/// <param name="Reference">The transfer or check number.</param>
public sealed record SupplierPaymentDto(
    Guid Id, decimal Amount, SupplierPaymentMethod Method, string? Reference, DateTimeOffset PaidAt, string PaidByName);

/// <summary>A credit invoice with something still owed.</summary>
/// <param name="DaysOverdue">Days since it fell due; 0 when it is not overdue yet.</param>
/// <param name="DaysToDue">Days left until it falls due; negative once overdue.</param>
public sealed record PayableInvoiceDto(
    Guid PurchaseId,
    Guid SupplierId,
    string SupplierName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal Total,
    decimal AmountPaid,
    decimal Balance,
    bool IsOverdue,
    int DaysOverdue,
    int? DaysToDue);

public sealed record PurchaseItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid SaleUnitId,
    string SaleUnitName,
    int UnitsPerPackage,
    int Quantity,
    int BonusQuantity,
    decimal UnitCost,
    decimal LineDiscount,
    VatRate VatRate,
    string LotNumber,
    DateOnly ExpirationDate,
    int BaseUnits,
    decimal NetAmount,
    Guid? BatchId,
    decimal? UnitCostPerBaseUnit);

/// <summary>One row of the purchases list (e.g. drafts to resume after a power cut).</summary>
public sealed record PurchaseSummaryDto(
    Guid Id,
    string SupplierName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    PurchaseStatus Status,
    int ItemCount,
    decimal Total);
