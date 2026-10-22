using Farmacia.Application.Products;
using Farmacia.Application.Products.CreateProduct;
using Farmacia.Application.Products.GetProductById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>Thin controller: translates HTTP to commands/queries and back. No business logic here.</summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    // The command is the request body. It is safe to bind directly because it carries no PharmacyId:
    // the tenant always comes from ICurrentPharmacy.
    [HttpPost]
    [ProducesResponseType<CreatedProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedProductResponse(id));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetProductByIdQuery(id), cancellationToken);
}

public sealed record CreatedProductResponse(Guid Id);
