using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Sales.GetSaleById;

/// <summary>A ticket, e.g. to print it again.</summary>
public sealed record GetSaleByIdQuery(Guid SaleId) : IRequest<SaleDto>;

public sealed class GetSaleByIdHandler(
    ISaleRepository sales,
    ICurrentPharmacy currentPharmacy,
    SaleReader reader) : IRequestHandler<GetSaleByIdQuery, SaleDto>
{
    public async Task<SaleDto> Handle(GetSaleByIdQuery query, CancellationToken cancellationToken)
    {
        var sale = await sales.GetByIdAsync(currentPharmacy.PharmacyId, query.SaleId, cancellationToken)
            ?? throw new NotFoundException("La venta no existe.");

        return await reader.ToDtoAsync(sale, cancellationToken);
    }
}
