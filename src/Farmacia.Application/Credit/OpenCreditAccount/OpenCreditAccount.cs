using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Credit;
using MediatR;

namespace Farmacia.Application.Credit.OpenCreditAccount;

/// <summary>The owner enables credit ("fiado") for a trusted customer, with a limit.</summary>
public sealed record OpenCreditAccountCommand(Guid CustomerId, decimal Limit) : IRequest;

public sealed class OpenCreditAccountHandler(
    ICustomerRepository customers,
    ICreditAccountRepository creditAccounts,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<OpenCreditAccountCommand>
{
    public async Task Handle(OpenCreditAccountCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        _ = await customers.GetByIdAsync(pharmacyId, command.CustomerId, cancellationToken)
            ?? throw new NotFoundException("El cliente no existe.");

        // The unique index (one account per customer) is the last line of defense; this gives a clear message.
        if (await creditAccounts.GetByCustomerAsync(pharmacyId, command.CustomerId, cancellationToken) is not null)
            throw new ConflictException("El cliente ya tiene fiado habilitado: cambie su cupo en vez de abrir otra cuenta.");

        creditAccounts.Add(CreditAccount.Open(pharmacyId, command.CustomerId, command.Limit));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
