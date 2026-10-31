using Farmacia.Api.Authentication;
using Farmacia.Application.Categories;
using Farmacia.Application.Categories.CreateCategory;
using Farmacia.Application.Categories.ListCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>Product categories (analgesics, antibiotics...). Anyone signed in lists them; the catalog managers create them.</summary>
[ApiController]
[Route("api/categories")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListCategoriesQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return Created($"/api/categories/{id}", new CreatedResponse(id));
    }
}
