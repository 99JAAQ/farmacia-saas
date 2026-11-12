using Farmacia.Application.Abstractions;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Pharmacies;
using MediatR;

namespace Farmacia.Application.Platform.CreatePharmacy;

/// <summary>A new pharmacy (tenant) with its owner, who then creates the rest of the users.</summary>
/// <param name="TaxId">NIT with its verification digit, e.g. "900123456-8".</param>
/// <param name="IsVatResponsible">Whether purchase VAT is deductible (it stays out of the cost). To confirm with the accountant.</param>
/// <param name="OwnerPassword">Initial password; the administrator gives it to the owner.</param>
public sealed record CreatePharmacyCommand(
    string Name,
    string TaxId,
    string? Address,
    string? Phone,
    bool IsVatResponsible,
    string OwnerFullName,
    string OwnerEmail,
    string OwnerPassword) : IRequest<Guid>;

public sealed class CreatePharmacyHandler(IPlatformPharmacies platform) : IRequestHandler<CreatePharmacyCommand, Guid>
{
    public async Task<Guid> Handle(CreatePharmacyCommand command, CancellationToken cancellationToken)
    {
        // The domain validates the name and the NIT (format and verification digit) before anything is written.
        var pharmacy = Pharmacy.Create(command.Name, command.TaxId, command.Address, command.Phone);
        pharmacy.SetVatResponsibility(command.IsVatResponsible);

        if (string.IsNullOrWhiteSpace(command.OwnerFullName))
            throw new ValidationException("Escriba el nombre del dueño.");
        if (string.IsNullOrWhiteSpace(command.OwnerEmail))
            throw new ValidationException("Escriba el email del dueño: con él inicia sesión.");
        if (string.IsNullOrEmpty(command.OwnerPassword))
            throw new ValidationException("Escriba la contraseña inicial del dueño.");

        var owner = new NewOwner(command.OwnerFullName.Trim(), command.OwnerEmail.Trim(), command.OwnerPassword);
        await platform.CreateAsync(pharmacy, owner, cancellationToken);

        return pharmacy.Id;
    }
}
