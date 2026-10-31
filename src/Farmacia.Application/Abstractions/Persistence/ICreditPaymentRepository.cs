using Farmacia.Domain.Credit;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICreditPaymentRepository
{
    Task<CreditPayment?> GetByIdAsync(Guid pharmacyId, Guid creditPaymentId, CancellationToken cancellationToken);

    void Add(CreditPayment payment);
}
