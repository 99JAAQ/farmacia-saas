using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Customers.SearchCustomers;

/// <summary>By name or document number, with their credit: the POS picks the customer before selling on credit.</summary>
public sealed record SearchCustomersQuery(string? Search) : IRequest<IReadOnlyList<CustomerDto>>;

public sealed class SearchCustomersHandler(
    ICustomerRepository customers,
    ICreditAccountRepository creditAccounts,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<SearchCustomersQuery, IReadOnlyList<CustomerDto>>
{
    // A pick list, not a report: enough to find one by typing a few letters.
    private const int MaxResults = 20;

    public async Task<IReadOnlyList<CustomerDto>> Handle(SearchCustomersQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var found = await customers.SearchAsync(pharmacyId, query.Search, MaxResults, cancellationToken);
        var accounts = (await creditAccounts.GetByCustomersAsync(pharmacyId, found.Select(c => c.Id).ToList(), cancellationToken))
            .ToDictionary(a => a.CustomerId);
        var today = DateOnly.FromDateTime(clock.Now.DateTime);

        return found.Select(c => CustomerDto.From(c, accounts.GetValueOrDefault(c.Id), today)).ToList();
    }
}
