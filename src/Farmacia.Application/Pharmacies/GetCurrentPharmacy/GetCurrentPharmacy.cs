using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Pharmacies.GetCurrentPharmacy;

/// <summary>The pharmacy of the signed-in user (from the token): there is no Id to ask for another one.</summary>
public sealed record GetCurrentPharmacyQuery : IRequest<PharmacyDto>;

public sealed class GetCurrentPharmacyHandler(IPharmacyRepository pharmacies, ICurrentPharmacy currentPharmacy)
    : IRequestHandler<GetCurrentPharmacyQuery, PharmacyDto>
{
    public async Task<PharmacyDto> Handle(GetCurrentPharmacyQuery query, CancellationToken cancellationToken)
    {
        var pharmacy = await pharmacies.GetByIdAsync(currentPharmacy.PharmacyId, cancellationToken)
            ?? throw new NotFoundException("No se encontró la farmacia.");

        return PharmacyDto.From(pharmacy);
    }
}
