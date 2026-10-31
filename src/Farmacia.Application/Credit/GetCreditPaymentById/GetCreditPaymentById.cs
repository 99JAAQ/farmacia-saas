using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Credit.GetCreditPaymentById;

/// <summary>A payment voucher, e.g. to print it again.</summary>
public sealed record GetCreditPaymentByIdQuery(Guid CreditPaymentId) : IRequest<CreditPaymentDto>;

public sealed class GetCreditPaymentByIdHandler(
    ICreditPaymentRepository payments,
    ICurrentPharmacy currentPharmacy,
    CreditPaymentReader reader) : IRequestHandler<GetCreditPaymentByIdQuery, CreditPaymentDto>
{
    public async Task<CreditPaymentDto> Handle(GetCreditPaymentByIdQuery query, CancellationToken cancellationToken)
    {
        var payment = await payments.GetByIdAsync(currentPharmacy.PharmacyId, query.CreditPaymentId, cancellationToken)
            ?? throw new NotFoundException("El abono no existe.");

        return await reader.ToDtoAsync(payment, cancellationToken);
    }
}
