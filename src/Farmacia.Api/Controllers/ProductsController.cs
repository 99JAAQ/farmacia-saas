using Farmacia.Api.Authentication;
using Farmacia.Application.Products;
using Farmacia.Application.Products.AddSaleUnit;
using Farmacia.Application.Products.CreateProduct;
using Farmacia.Application.Products.GetCatalog;
using Farmacia.Application.Products.GetProductBatches;
using Farmacia.Application.Products.GetProductById;
using Farmacia.Application.Products.SearchProducts;
using Farmacia.Application.Products.SetMinimumStock;
using Farmacia.Application.Products.SetProductActive;
using Farmacia.Application.Products.UpdateProduct;
using Farmacia.Application.Products.UpdateSaleUnit;
using Farmacia.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>Thin controller: translates HTTP to commands/queries and back. No business logic here.</summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    // The command is the request body. It is safe to bind directly because it carries no PharmacyId:
    // the tenant always comes from ICurrentPharmacy (the signed token).
    [HttpPost]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }

    /// <summary>
    /// By name or active ingredient (ignoring case and accents) or by exact barcode. Any logged-in user (the POS).
    /// Only active products unless includeInactive: a discontinued product is never offered for sale.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SearchProductsQuery(search, includeInactive), cancellationToken));

    /// <summary>The catalog screen: products with their stock (sellable and expired units, nearest expiration).</summary>
    [HttpGet("catalog")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType<IReadOnlyList<CatalogItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Catalog(
        [FromQuery] string? search, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetCatalogQuery(search, includeInactive), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetProductByIdQuery(id), cancellationToken);

    /// <summary>The product's batches with units, the first to expire first, with their cost: not for the cashier.</summary>
    [HttpGet("{id:guid}/batches")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType<IReadOnlyList<BatchDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Batches(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetProductBatchesQuery(id), cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateProductCommand(
            id, request.Name, request.BaseUnitName, request.RequiresPrescription, request.ActiveIngredient, request.Concentration,
            request.PharmaceuticalForm, request.SanitaryRegistration, request.CategoryId, request.VatRate), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/sale-units")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSaleUnit(Guid id, SaleUnitRequest request, CancellationToken cancellationToken)
    {
        var unitId = await sender.Send(
            new AddSaleUnitCommand(id, request.Name, request.ConversionFactor, request.Price, request.Barcode), cancellationToken);

        // The unit has no URL of its own: Location points to the product that now includes it.
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(unitId));
    }

    /// <summary>A new price applies to the next sales only: every sale kept its own.</summary>
    [HttpPut("{id:guid}/sale-units/{saleUnitId:guid}")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSaleUnit(
        Guid id, Guid saleUnitId, SaleUnitRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSaleUnitCommand(id, saleUnitId, request.Name, request.ConversionFactor, request.Price, request.Barcode),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Discontinues the product: it is no longer sold nor bought. Its history stays.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SetProductActiveCommand(id, Active: false), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SetProductActiveCommand(id, Active: true), cancellationToken);
        return NoContent();
    }

    /// <summary>Below this stock (in base units) the product shows up in the stock alerts; null removes it.</summary>
    [HttpPut("{id:guid}/minimum-stock")]
    [Authorize(Policy = Policies.ManageCatalog)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetMinimumStock(Guid id, MinimumStockRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetMinimumStockCommand(id, request.MinimumStock), cancellationToken);
        return NoContent();
    }
}

/// <param name="MinimumStock">In base units (tablets); null removes it.</param>
public sealed record MinimumStockRequest(int? MinimumStock);

public sealed record UpdateProductRequest(
    string Name,
    string BaseUnitName,
    bool RequiresPrescription,
    string? ActiveIngredient,
    string? Concentration,
    string? PharmaceuticalForm,
    string? SanitaryRegistration,
    Guid? CategoryId,
    VatRate VatRate);

/// <param name="ConversionFactor">How many base units it contains (a box of 30 tablets = 30).</param>
public sealed record SaleUnitRequest(string Name, int ConversionFactor, decimal Price, string? Barcode);
