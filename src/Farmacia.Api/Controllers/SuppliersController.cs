using Farmacia.Api.Authentication;
using Farmacia.Application.Suppliers;
using Farmacia.Application.Suppliers.CreateSupplier;
using Farmacia.Application.Suppliers.GetSupplierById;
using Farmacia.Application.Suppliers.SearchSuppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = Policies.ManageCatalog)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class SuppliersController(ISender sender) : ControllerBase
{
    /// <summary>By name (ignoring case and accents) or by NIT, with or without its verification digit.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SupplierDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SearchSuppliersQuery(search), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SupplierDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetSupplierByIdQuery(id), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }
}
