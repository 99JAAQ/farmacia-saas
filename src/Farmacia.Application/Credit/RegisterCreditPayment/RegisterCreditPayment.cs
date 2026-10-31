using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Cash;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Sales;
using MediatR;

namespace Farmacia.Application.Credit.RegisterCreditPayment;

/// <summary>An "abono": a customer pays part or all of what they owe.</summary>
public sealed record RegisterCreditPaymentCommand(Guid CustomerId, PaymentMethod Method, decimal Amount)
    : IRequest<CreditPaymentDto>;

public sealed class RegisterCreditPaymentHandler(
    ICashSessionRepository sessions,
    ICreditAccountRepository creditAccounts,
    ICreditPaymentRepository payments,
    IDocumentNumberSequence documentNumbers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock,
    CreditPaymentReader reader) : IRequestHandler<RegisterCreditPaymentCommand, CreditPaymentDto>
{
    public async Task<CreditPaymentDto> Handle(RegisterCreditPaymentCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        // In cash, the payment goes into the drawer of whoever receives it: it needs their own open shift.
        var session = await sessions.GetOpenOrThrowAsync(pharmacyId, cancellationToken);
        if (session.OpenedBy != currentUser.UserId)
            throw new ForbiddenException("La caja está abierta por otro cajero: solo esa persona puede recibir abonos en este turno.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var number = await documentNumbers.NextAsync(pharmacyId, NumberedDocument.CreditPayment, cancellationToken);

        // Read after the number: the balance is as fresh as it can be. A sale charging the same account at the same
        // time is caught by the account's RowVersion (409).
        var account = await creditAccounts.GetByCustomerAsync(pharmacyId, command.CustomerId, cancellationToken)
            ?? throw new NotFoundException("El cliente no tiene cuenta de fiado.");

        var payment = CreditPayment.Register(
            number, account, session.Id, currentUser.UserId, clock.Now, command.Method, command.Amount);
        payments.Add(payment);

        // Payment, account and its entry in one SaveChanges.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await reader.ToDtoAsync(payment, cancellationToken);
    }
}
