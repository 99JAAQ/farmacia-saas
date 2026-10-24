using Farmacia.Domain.Common;
using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Purchases;

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
    IReadOnlyList<PurchaseItemDto> Items);

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
