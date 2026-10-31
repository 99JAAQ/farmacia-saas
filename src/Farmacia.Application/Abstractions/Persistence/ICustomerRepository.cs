using Farmacia.Domain.Customers;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken);

    void Add(Customer customer);
}
