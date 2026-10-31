using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Customers;
using MediatR;

namespace Farmacia.Application.Customers.CreateCustomer;

/// <summary>The owner registers a customer (e.g. to give them credit). No public sign-up.</summary>
public sealed record CreateCustomerCommand(
    string FullName,
    DocumentType DocumentType,
    string DocumentNumber,
    string? Phone,
    string? Email) : IRequest<Guid>;

public sealed class CreateCustomerHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        // The domain validates the document (a NIT with its verification digit) before any query.
        var customer = Customer.Create(
            currentPharmacy.PharmacyId, command.FullName, command.DocumentType, command.DocumentNumber, command.Phone, command.Email);

        if (await customers.DocumentExistsAsync(customer.PharmacyId, customer.DocumentType, customer.DocumentNumber, cancellationToken))
            throw new ConflictException($"Ya existe un cliente con el documento {customer.DocumentNumber}.");

        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
