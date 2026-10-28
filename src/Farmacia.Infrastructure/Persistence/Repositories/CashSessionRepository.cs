using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Cash;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CashSessionRepository(FarmaciaDbContext db) : ICashSessionRepository
{
    public Task<CashSession?> GetByIdAsync(Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        db.CashSessions
            .Include(s => s.Movements)
            .SingleOrDefaultAsync(s => s.PharmacyId == pharmacyId && s.Id == cashSessionId, cancellationToken);

    public Task<CashSession?> GetOpenAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        db.CashSessions
            .Include(s => s.Movements)
            .SingleOrDefaultAsync(s => s.PharmacyId == pharmacyId && s.Status == CashSessionStatus.Open, cancellationToken);

    public Task<CashSession?> GetLastClosedAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
        db.CashSessions
            .Where(s => s.PharmacyId == pharmacyId && s.Status == CashSessionStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(CashSession cashSession) => db.CashSessions.Add(cashSession);
}
