using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Products.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDto>;

public sealed class GetProductByIdHandler(
    IProductRepository products,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        // A product of another pharmacy is reported as "not found", never as "forbidden":
        // that would reveal that the Id exists.
        var product = await products.GetByIdAsync(currentPharmacy.PharmacyId, query.ProductId, cancellationToken)
            ?? throw new NotFoundException("El producto no existe.");

        return ProductDto.From(product);
    }
}
