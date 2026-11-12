using Farmacia.Api.Authentication;
using Farmacia.Application.Inventory.AdjustBatchToCount;
using Farmacia.Application.Inventory.GetProductKardex;
using Farmacia.Application.Inventory.GetStockAlerts;
using Farmacia.Application.Inventory.WriteOffExpiredBatch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>Stock outside purchases and sales: write-offs, physical counts and the kardex. Owner and stock keeper.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.ManageInventory)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class InventoryController(ISender sender) : ControllerBase
{
    /// <summary>
    /// What needs attention: products below their minimum, expired batches with units (to write off) and batches that
    /// expire within 90 days. The screens refresh it on every "stock" notice (real time).
    /// </summary>
    [HttpGet("stock-alerts")]
    [ProducesResponseType<StockAlertsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockAlertsDto>> StockAlerts(CancellationToken cancellationToken) =>
        await sender.Send(new GetStockAlertsQuery(), cancellationToken);

    /// <summary>A product's latest movements, newest first, with the product's and the batch's balance after each.</summary>
    [HttpGet("products/{id:guid}/kardex")]
    [ProducesResponseType<IReadOnlyList<KardexEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Kardex(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetProductKardexQuery(id), cancellationToken));

    /// <summary>Takes all the units of an expired batch out of the inventory.</summary>
    /// <response code="400">The batch has not expired yet, or has no units.</response>
    /// <response code="409">A sale took from the same batch at the same time.</response>
    [HttpPost("batches/{id:guid}/write-off")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WriteOff(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new WriteOffExpiredBatchCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Sets the batch to what was physically counted, in base units, with the reason.</summary>
    /// <response code="400">Negative count, no reason, or the count matches the stock.</response>
    [HttpPost("batches/{id:guid}/adjust")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Adjust(Guid id, AdjustBatchRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new AdjustBatchToCountCommand(id, request.CountedQuantity, request.Reason), cancellationToken);
        return NoContent();
    }
}

/// <param name="CountedQuantity">In base units (e.g. tablets), as counted on the shelf.</param>
public sealed record AdjustBatchRequest(int CountedQuantity, string Reason);
