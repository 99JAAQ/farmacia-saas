using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Products;
using MediatR;

namespace Farmacia.Application.Categories.CreateCategory;

public sealed record CreateCategoryCommand(string Name) : IRequest<Guid>;

public sealed class CreateCategoryHandler(
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = Category.Create(currentPharmacy.PharmacyId, command.Name);

        if (await categories.NameExistsAsync(category.PharmacyId, category.Name, cancellationToken))
            throw new ConflictException($"Ya existe la categoría {category.Name}.");

        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
