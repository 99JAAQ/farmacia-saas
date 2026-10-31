using Farmacia.Api.Authentication;
using Farmacia.Application.Sales;
using Farmacia.Application.Sales.GetSaleById;
using Farmacia.Application.Sales.GetSaleByNumber;
using Farmacia.Application.Sales.ListSales;
using Farmacia.Application.Sales.RegisterSale;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// Sales at the register. The prices, the cashier, the shift and the time are never in the body: they come from the
/// catalog, the token, the open shift and the server clock.
/// </summary>
[ApiController]
[Route("api/sales")]
[Authorize(Policy = Policies.OperateCash)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class SalesController(ISender sender) : ControllerBase
{
    /// <summary>A shift's sales, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SaleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SaleSummaryDto>>> List(
        [FromQuery] Guid cashSessionId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListSalesQuery(cashSessionId), cancellationToken));

    /// <summary>The ticket, e.g. to print it again.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SaleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetSaleByIdQuery(id), cancellationToken);

    /// <summary>A ticket by the number printed on it: how a return starts at the counter.</summary>
    [HttpGet("by-number/{number:long}")]
    [ProducesResponseType<SaleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleDto>> GetByNumber(long number, CancellationToken cancellationToken) =>
        await sender.Send(new GetSaleByNumberQuery(number), cancellationToken);

    /// <summary>
    /// "Cobrar": the whole cart at once, in the current user's open shift. Answers with the whole ticket (not just
    /// the id, as other creations do) because the POS prints it right away.
    /// </summary>
    /// <response code="400">Invalid cart or payments, not enough stock, or a prescription not checked.</response>
    /// <response code="409">No open shift, or another sale took the same units first.</response>
    [HttpPost]
    [ProducesResponseType<SaleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterSaleRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new RegisterSaleCommand(request.Lines, request.Payments, request.PrescriptionChecked, request.CustomerId), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }
}

/// <param name="Payments">For cash, the amount the customer handed over; the change is computed.</param>
/// <param name="CustomerId">Required to take part of the sale on credit ("Credit" payment).</param>
public sealed record RegisterSaleRequest(
    IReadOnlyList<RegisterSaleLine> Lines,
    IReadOnlyList<RegisterSalePayment> Payments,
    bool PrescriptionChecked,
    Guid? CustomerId = null);
