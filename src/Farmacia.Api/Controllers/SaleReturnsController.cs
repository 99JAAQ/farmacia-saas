using Farmacia.Api.Authentication;
using Farmacia.Application.SaleReturns;
using Farmacia.Application.SaleReturns.GetSaleReturnById;
using Farmacia.Application.SaleReturns.RegisterSaleReturn;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// Returns at the register: the cash comes out of the current user's open shift. The prices refunded, the cashier,
/// the shift and the time are never in the body: they come from the ticket, the token, the open shift and the clock.
/// </summary>
[ApiController]
[Route("api/sale-returns")]
[Authorize(Policy = Policies.OperateCash)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class SaleReturnsController(ISender sender) : ControllerBase
{
    /// <summary>The return voucher, e.g. to print it again.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SaleReturnDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleReturnDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetSaleReturnByIdQuery(id), cancellationToken);

    /// <summary>
    /// Returns part or all of a ticket. Answers with the whole voucher (like a sale answers with its ticket) because
    /// the register prints it right away.
    /// </summary>
    /// <response code="400">More than is left to return, no reason, or units into an expired batch.</response>
    /// <response code="404">The ticket does not exist (or is another pharmacy's).</response>
    /// <response code="409">No open shift.</response>
    [HttpPost]
    [ProducesResponseType<SaleReturnDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterSaleReturnRequest request, CancellationToken cancellationToken)
    {
        var voucher = await sender.Send(
            new RegisterSaleReturnCommand(request.SaleId, request.Reason, request.Lines), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = voucher.Id }, voucher);
    }
}

/// <param name="Lines">Ticket lines to return, each with its quantity (in the sale unit) and Restock or Discard.</param>
public sealed record RegisterSaleReturnRequest(Guid SaleId, string Reason, IReadOnlyList<RegisterSaleReturnLine> Lines);
