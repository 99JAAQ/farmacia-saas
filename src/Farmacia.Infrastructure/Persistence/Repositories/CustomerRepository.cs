using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(FarmaciaDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.SingleOrDefaultAsync(c => c.PharmacyId == pharmacyId && c.Id == customerId, cancellationToken);

    public async Task<IReadOnlyList<Customer>> GetByIdsAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken) =>
        await db.Customers
            .Where(c => c.PharmacyId == pharmacyId && customerIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Customer>> SearchAsync(
        Guid pharmacyId, string? search, int take, CancellationToken cancellationToken)
    {
        var query = db.Customers.Where(c => c.PharmacyId == pharmacyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = SearchPattern.Contains(search);
            var document = search.Trim();
            query = query.Where(c =>
                EF.Functions.Like(EF.Functions.Collate(c.FullName, SearchPattern.Collation), pattern)
                || c.DocumentNumber == document);
        }

        return await query.OrderBy(c => c.FullName).Take(take).ToListAsync(cancellationToken);
    }

    public Task<bool> DocumentExistsAsync(
        Guid pharmacyId, DocumentType documentType, string documentNumber, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(
            c => c.PharmacyId == pharmacyId && c.DocumentType == documentType && c.DocumentNumber == documentNumber,
            cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);
}
