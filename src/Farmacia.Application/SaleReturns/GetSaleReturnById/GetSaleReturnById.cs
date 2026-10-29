using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.SaleReturns.GetSaleReturnById;

/// <summary>A return voucher, e.g. to print it again.</summary>
public sealed record GetSaleReturnByIdQuery(Guid SaleReturnId) : IRequest<SaleReturnDto>;

public sealed class GetSaleReturnByIdHandler(
    ISaleReturnRepository returns,
    ICurrentPharmacy currentPharmacy,
    SaleReturnReader reader) : IRequestHandler<GetSaleReturnByIdQuery, SaleReturnDto>
{
    public async Task<SaleReturnDto> Handle(GetSaleReturnByIdQuery query, CancellationToken cancellationToken)
    {
        var saleReturn = await returns.GetByIdAsync(currentPharmacy.PharmacyId, query.SaleReturnId, cancellationToken)
            ?? throw new NotFoundException("La devolución no existe.");

        return await reader.ToDtoAsync(saleReturn, sale: null, cancellationToken);
    }
}
