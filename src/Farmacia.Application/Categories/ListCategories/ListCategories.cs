using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Categories.ListCategories;

public sealed record ListCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

public sealed class ListCategoriesHandler(
    ICategoryRepository categories,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(ListCategoriesQuery query, CancellationToken cancellationToken) =>
        (await categories.ListAsync(currentPharmacy.PharmacyId, cancellationToken))
            .Select(c => new CategoryDto(c.Id, c.Name))
            .ToList();
}
