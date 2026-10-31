using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Credit.ListDebtors;

/// <summary>Who owes, the oldest debt first: the owner's list of whom to call.</summary>
public sealed record ListDebtorsQuery : IRequest<IReadOnlyList<DebtorDto>>;

public sealed class ListDebtorsHandler(
    ICustomerRepository customers,
    ICreditAccountRepository creditAccounts,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<ListDebtorsQuery, IReadOnlyList<DebtorDto>>
{
    // A pharmacy's credit customers fit in one page; paging comes with the reports if ever needed.
    private const int MaxResults = 200;

    public async Task<IReadOnlyList<DebtorDto>> Handle(ListDebtorsQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var accounts = await creditAccounts.ListDebtorsAsync(pharmacyId, MaxResults, cancellationToken);
        var names = (await customers.GetByIdsAsync(pharmacyId, accounts.Select(a => a.CustomerId).ToList(), cancellationToken))
            .ToDictionary(c => c.Id);
        var today = DateOnly.FromDateTime(clock.Now.DateTime);

        return accounts
            .Select(a =>
            {
                var customer = names.GetValueOrDefault(a.CustomerId);
                return new DebtorDto(
                    a.CustomerId, customer?.FullName ?? "Cliente desconocido", customer?.DocumentNumber ?? "", customer?.Phone,
                    CreditAccountDto.From(a, today));
            })
            .ToList();
    }
}
