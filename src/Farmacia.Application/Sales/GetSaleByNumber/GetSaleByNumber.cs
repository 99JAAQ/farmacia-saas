using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Sales.GetSaleByNumber;

/// <summary>A ticket by the number printed on it: how a return starts at the counter.</summary>
public sealed record GetSaleByNumberQuery(long Number) : IRequest<SaleDto>;

public sealed class GetSaleByNumberHandler(
    ISaleRepository sales,
    ICurrentPharmacy currentPharmacy,
    SaleReader reader) : IRequestHandler<GetSaleByNumberQuery, SaleDto>
{
    public async Task<SaleDto> Handle(GetSaleByNumberQuery query, CancellationToken cancellationToken)
    {
        var sale = await sales.GetByNumberAsync(currentPharmacy.PharmacyId, query.Number, cancellationToken)
            ?? throw new NotFoundException($"No existe el tiquete N.º {query.Number}.");

        return await reader.ToDtoAsync(sale, cancellationToken);
    }
}
