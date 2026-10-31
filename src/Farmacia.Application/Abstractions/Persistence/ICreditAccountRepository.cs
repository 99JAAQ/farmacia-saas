using Farmacia.Domain.Credit;

namespace Farmacia.Application.Abstractions.Persistence;

public interface ICreditAccountRepository
{
    /// <summary>Tracked: charging, paying or crediting it is saved, with its entries, by the next SaveChanges.</summary>
    Task<CreditAccount?> GetByCustomerAsync(Guid pharmacyId, Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditAccount>> GetByCustomersAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);

    /// <summary>The accounts that owe something, the oldest debt first: who to call.</summary>
    Task<IReadOnlyList<CreditAccount>> ListDebtorsAsync(Guid pharmacyId, int take, CancellationToken cancellationToken);

    /// <summary>The account's history, newest first.</summary>
    Task<IReadOnlyList<CreditEntry>> ListEntriesAsync(Guid pharmacyId, Guid creditAccountId, int take, CancellationToken cancellationToken);

    void Add(CreditAccount account);
}
