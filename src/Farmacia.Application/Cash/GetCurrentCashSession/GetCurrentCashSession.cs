using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Cash.GetCurrentCashSession;

/// <summary>The open shift (if any) and the float to suggest when opening the next one.</summary>
public sealed record GetCurrentCashSessionQuery : IRequest<CurrentCashSessionDto>;

public sealed class GetCurrentCashSessionHandler(
    ICashSessionRepository sessions,
    ICurrentPharmacy currentPharmacy,
    CashSessionReader reader) : IRequestHandler<GetCurrentCashSessionQuery, CurrentCashSessionDto>
{
    public async Task<CurrentCashSessionDto> Handle(GetCurrentCashSessionQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        if (await sessions.GetOpenAsync(pharmacyId, cancellationToken) is { } open)
            return new CurrentCashSessionDto(await reader.ToDtoAsync(open, cancellationToken), SuggestedOpeningFloat: 0m);

        var lastClosed = await sessions.GetLastClosedAsync(pharmacyId, cancellationToken);
        return new CurrentCashSessionDto(Session: null, SuggestedOpeningFloat: lastClosed?.CountedCash ?? 0m);
    }
}
