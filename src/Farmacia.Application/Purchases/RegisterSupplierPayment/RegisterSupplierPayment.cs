using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Domain.Purchases;
using MediatR;

namespace Farmacia.Application.Purchases.RegisterSupplierPayment;

/// <summary>The owner pays part or all of a credit invoice, outside the register (transfer, safe or check).</summary>
/// <param name="Reference">The transfer or check number (required for those two).</param>
public sealed record RegisterSupplierPaymentCommand(Guid PurchaseId, decimal Amount, SupplierPaymentMethod Method, string? Reference)
    : IRequest<Guid>;

public sealed class RegisterSupplierPaymentHandler(
    IPurchaseRepository purchases,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RegisterSupplierPaymentCommand, Guid>
{
    public async Task<Guid> Handle(RegisterSupplierPaymentCommand command, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetOrThrowAsync(currentPharmacy.PharmacyId, command.PurchaseId, cancellationToken);

        var payment = purchase.RegisterPayment(command.Amount, command.Method, command.Reference, clock.Now, currentUser.UserId);

        // The payment updates the purchase's AmountPaid: a simultaneous payment on the same invoice gets a 409.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }
}
