using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Cash;

namespace Farmacia.Application.Cash;

internal static class CashSessionRepositoryExtensions
{
    public static async Task<CashSession> GetOrThrowAsync(
        this ICashSessionRepository sessions, Guid pharmacyId, Guid cashSessionId, CancellationToken cancellationToken) =>
        await sessions.GetByIdAsync(pharmacyId, cashSessionId, cancellationToken)
            ?? throw new NotFoundException("El turno de caja no existe.");

    public static async Task<CashSession> GetOpenOrThrowAsync(
        this ICashSessionRepository sessions, Guid pharmacyId, CancellationToken cancellationToken) =>
        await sessions.GetOpenAsync(pharmacyId, cancellationToken)
            ?? throw new ConflictException("No hay un turno de caja abierto. Abra uno primero.");
}
