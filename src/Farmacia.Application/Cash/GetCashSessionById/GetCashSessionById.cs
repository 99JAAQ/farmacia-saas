using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Cash.GetCashSessionById;

/// <summary>Any shift, open or closed. The blind count applies here too (<see cref="CashSessionReader"/>).</summary>
public sealed record GetCashSessionByIdQuery(Guid CashSessionId) : IRequest<CashSessionDto>;

public sealed class GetCashSessionByIdHandler(
    ICashSessionRepository sessions,
    ICurrentPharmacy currentPharmacy,
    CashSessionReader reader) : IRequestHandler<GetCashSessionByIdQuery, CashSessionDto>
{
    public async Task<CashSessionDto> Handle(GetCashSessionByIdQuery query, CancellationToken cancellationToken)
    {
        var session = await sessions.GetOrThrowAsync(currentPharmacy.PharmacyId, query.CashSessionId, cancellationToken);
        return await reader.ToDtoAsync(session, cancellationToken);
    }
}
