using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Pharmacies.UpdatePharmacy;

/// <summary>The owner updates the pharmacy's name, address and phone (printed on tickets) and its VAT setting.</summary>
/// <param name="IsVatResponsible">
/// Whether purchase VAT is deductible. It applies to purchases confirmed from now on: batches already received keep
/// the cost they were received with.
/// </param>
public sealed record UpdatePharmacyCommand(string Name, string? Address, string? Phone, bool IsVatResponsible) : IRequest;

public sealed class UpdatePharmacyHandler(
    IPharmacyRepository pharmacies,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<UpdatePharmacyCommand>
{
    public async Task Handle(UpdatePharmacyCommand command, CancellationToken cancellationToken)
    {
        var pharmacy = await pharmacies.GetByIdAsync(currentPharmacy.PharmacyId, cancellationToken)
            ?? throw new NotFoundException("No se encontró la farmacia.");

        pharmacy.UpdateDetails(command.Name, command.Address, command.Phone);
        pharmacy.SetVatResponsibility(command.IsVatResponsible);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
