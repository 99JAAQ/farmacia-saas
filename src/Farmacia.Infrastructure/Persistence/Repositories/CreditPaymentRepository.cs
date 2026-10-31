using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Credit;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CreditPaymentRepository(FarmaciaDbContext db) : ICreditPaymentRepository
{
    public Task<CreditPayment?> GetByIdAsync(Guid pharmacyId, Guid creditPaymentId, CancellationToken cancellationToken) =>
        db.CreditPayments.SingleOrDefaultAsync(p => p.PharmacyId == pharmacyId && p.Id == creditPaymentId, cancellationToken);

    public void Add(CreditPayment payment) => db.CreditPayments.Add(payment);
}
