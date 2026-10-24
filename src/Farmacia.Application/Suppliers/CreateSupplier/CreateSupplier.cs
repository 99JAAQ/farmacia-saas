using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Suppliers;
using MediatR;

namespace Farmacia.Application.Suppliers.CreateSupplier;

/// <param name="TaxId">NIT with its verification digit, e.g. "800987654-4".</param>
public sealed record CreateSupplierCommand(
    string Name,
    string TaxId,
    string? ContactName,
    string? Phone,
    string? Email) : IRequest<Guid>;

public sealed class CreateSupplierHandler(
    ISupplierRepository suppliers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<CreateSupplierCommand, Guid>
{
    public async Task<Guid> Handle(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        // The domain validates the NIT (format and verification digit) before any query.
        var supplier = Supplier.Create(
            currentPharmacy.PharmacyId, command.Name, command.TaxId, command.ContactName, command.Phone, command.Email);

        if (await suppliers.TaxIdExistsAsync(supplier.PharmacyId, supplier.TaxId, cancellationToken))
            throw new ConflictException($"Ya existe un proveedor con el NIT {supplier.TaxId}.");

        suppliers.Add(supplier);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}
