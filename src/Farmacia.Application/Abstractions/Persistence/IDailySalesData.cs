using Farmacia.Domain.Common;
using Farmacia.Domain.Sales;

namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>A pharmacy's sales, payments and returns of one day (Colombia), as plain rows: few, and read-only.</summary>
public interface IDailySalesData
{
    Task<DailySalesData> GetAsync(Guid pharmacyId, DateOnly date, CancellationToken cancellationToken);
}

public sealed record DailySalesData(
    IReadOnlyList<DailySaleLine> Lines,
    IReadOnlyList<DailyPayment> Payments,
    IReadOnlyList<DailyReturnLine> Returns);

/// <param name="Hour">Local hour of the sale (0-23, Colombia).</param>
/// <param name="Cost">What the units cost: the sum of their batches' quantity × unit cost.</param>
public sealed record DailySaleLine(
    Guid SaleId, int Hour, Guid ProductId, int BaseUnits, decimal Total, VatRate VatRate, decimal Cost);

/// <param name="Amount">What the payment applied to the sale (cash without the change).</param>
public sealed record DailyPayment(PaymentMethod Method, decimal Amount);

/// <param name="RestockedCost">Cost of the units that went back to a batch (none if they were discarded).</param>
public sealed record DailyReturnLine(decimal Refund, VatRate VatRate, decimal RestockedCost);
