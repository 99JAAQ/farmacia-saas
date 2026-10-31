using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Credit;

namespace Farmacia.Application.Credit;

/// <summary>Builds the voucher DTO with the customer and cashier names (they live in other aggregates).</summary>
public sealed class CreditPaymentReader(ICustomerRepository customers, IUserReader users)
{
    public async Task<CreditPaymentDto> ToDtoAsync(CreditPayment payment, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(payment.PharmacyId, payment.CustomerId, cancellationToken);
        var names = await users.GetNamesAsync(payment.PharmacyId, [payment.ReceivedBy], cancellationToken);

        return new CreditPaymentDto(
            payment.Id,
            payment.Number,
            payment.CustomerId,
            customer?.FullName ?? "Cliente desconocido",
            payment.ReceivedAt,
            payment.CashSessionId,
            names.GetValueOrDefault(payment.ReceivedBy, "Usuario desconocido"),
            payment.Method,
            payment.Amount,
            payment.BalanceAfter);
    }
}
