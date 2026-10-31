using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid CustomerId) : IRequest<CustomerDto>;

public sealed class GetCustomerByIdHandler(
    ICustomerRepository customers,
    ICreditAccountRepository creditAccounts,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<GetCustomerByIdQuery, CustomerDto>
{
    public async Task<CustomerDto> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var customer = await customers.GetByIdAsync(pharmacyId, query.CustomerId, cancellationToken)
            ?? throw new NotFoundException("El cliente no existe.");
        var account = await creditAccounts.GetByCustomerAsync(pharmacyId, customer.Id, cancellationToken);

        return CustomerDto.From(customer, account, DateOnly.FromDateTime(clock.Now.DateTime));
    }
}
