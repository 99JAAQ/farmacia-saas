using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(FarmaciaDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.SingleOrDefaultAsync(c => c.PharmacyId == pharmacyId && c.Id == customerId, cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);
}
