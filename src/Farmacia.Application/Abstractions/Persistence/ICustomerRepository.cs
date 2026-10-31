using Farmacia.Domain.Customers;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Customer>> GetByIdsAsync(Guid pharmacyId, IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);

    /// <summary>By name (ignoring case and accents) or by exact document number; all of them, by name, if empty.</summary>
    Task<IReadOnlyList<Customer>> SearchAsync(Guid pharmacyId, string? search, int take, CancellationToken cancellationToken);

    Task<bool> DocumentExistsAsync(Guid pharmacyId, DocumentType documentType, string documentNumber, CancellationToken cancellationToken);

    void Add(Customer customer);
}
