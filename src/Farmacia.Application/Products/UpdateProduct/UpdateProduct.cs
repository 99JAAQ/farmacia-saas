using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Common;
using MediatR;

namespace Farmacia.Application.Products.UpdateProduct;

/// <summary>Corrects or completes a product's data. Its presentations are changed one by one.</summary>
public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string BaseUnitName,
    bool RequiresPrescription,
    string? ActiveIngredient,
    string? Concentration,
    string? PharmaceuticalForm,
    string? SanitaryRegistration,
    Guid? CategoryId,
    VatRate VatRate) : IRequest;

public sealed class UpdateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var product = await products.GetOrThrowAsync(pharmacyId, command.ProductId, cancellationToken);

        if (command.CategoryId is { } categoryId && !await categories.ExistsAsync(pharmacyId, categoryId, cancellationToken))
            throw new ValidationException("La categoría indicada no existe.");

        product.UpdateDetails(
            command.Name, command.BaseUnitName, command.RequiresPrescription, command.ActiveIngredient, command.Concentration,
            command.PharmaceuticalForm, command.SanitaryRegistration, command.CategoryId, command.VatRate);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
