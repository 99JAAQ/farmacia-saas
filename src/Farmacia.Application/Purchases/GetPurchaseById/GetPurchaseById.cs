using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Purchases.GetPurchaseById;

public sealed record GetPurchaseByIdQuery(Guid PurchaseId) : IRequest<PurchaseDto>;

public sealed class GetPurchaseByIdHandler(
    IPurchaseRepository purchases,
    ICurrentPharmacy currentPharmacy,
    PurchaseReader reader) : IRequestHandler<GetPurchaseByIdQuery, PurchaseDto>
{
    public async Task<PurchaseDto> Handle(GetPurchaseByIdQuery query, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetOrThrowAsync(currentPharmacy.PharmacyId, query.PurchaseId, cancellationToken);
        return await reader.ToDtoAsync(purchase, cancellationToken);
    }
}
