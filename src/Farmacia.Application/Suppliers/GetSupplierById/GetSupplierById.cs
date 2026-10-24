using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Suppliers.GetSupplierById;

public sealed record GetSupplierByIdQuery(Guid SupplierId) : IRequest<SupplierDto>;

public sealed class GetSupplierByIdHandler(
    ISupplierRepository suppliers,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<GetSupplierByIdQuery, SupplierDto>
{
    public async Task<SupplierDto> Handle(GetSupplierByIdQuery query, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.GetByIdAsync(currentPharmacy.PharmacyId, query.SupplierId, cancellationToken)
            ?? throw new NotFoundException("El proveedor no existe.");

        return SupplierDto.From(supplier);
    }
}
