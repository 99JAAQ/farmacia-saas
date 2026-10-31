using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Credit.GetCreditStatement;

/// <summary>The customer's balance and its latest movements.</summary>
public sealed record GetCreditStatementQuery(Guid CustomerId) : IRequest<CreditStatementDto>;

public sealed class GetCreditStatementHandler(
    ICustomerRepository customers,
    ICreditAccountRepository creditAccounts,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<GetCreditStatementQuery, CreditStatementDto>
{
    // Enough to explain the current balance at the counter; a full history report comes later.
    private const int MaxEntries = 100;

    public async Task<CreditStatementDto> Handle(GetCreditStatementQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var customer = await customers.GetByIdAsync(pharmacyId, query.CustomerId, cancellationToken)
            ?? throw new NotFoundException("El cliente no existe.");
        var account = await creditAccounts.GetOrThrowAsync(pharmacyId, customer.Id, cancellationToken);
        var entries = await creditAccounts.ListEntriesAsync(pharmacyId, account.Id, MaxEntries, cancellationToken);

        return new CreditStatementDto(
            customer.Id,
            customer.FullName,
            CreditAccountDto.From(account, DateOnly.FromDateTime(clock.Now.DateTime)),
            entries.Select(e => new CreditEntryDto(e.Type, e.Change, e.BalanceAfter, e.OccurredAt, e.ReferenceId)).ToList());
    }
}
