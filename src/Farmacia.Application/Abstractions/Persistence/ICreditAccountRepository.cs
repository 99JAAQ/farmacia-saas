using Farmacia.Domain.Credit;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICreditAccountRepository
{
    /// <summary>Tracked: charging, paying or crediting it is saved, with its entries, by the next SaveChanges.</summary>
    Task<CreditAccount?> GetByCustomerAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken);

    void Add(CreditAccount account);
}
