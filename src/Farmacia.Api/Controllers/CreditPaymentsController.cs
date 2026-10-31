using Farmacia.Api.Authentication;
using Farmacia.Application.Credit;
using Farmacia.Application.Credit.GetCreditPaymentById;
using Farmacia.Application.Credit.RegisterCreditPayment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>Payments towards a customer's credit ("abonos"), received in the current user's open shift.</summary>
[ApiController]
[Route("api/credit-payments")]
[Authorize(Policy = Policies.OperateCash)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CreditPaymentsController(ISender sender) : ControllerBase
{
    /// <summary>The payment voucher, e.g. to print it again.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CreditPaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreditPaymentDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetCreditPaymentByIdQuery(id), cancellationToken);

    /// <summary>Answers with the whole voucher (like a sale with its ticket): it is printed right away.</summary>
    /// <response code="400">Over the debt, zero, or paid "on credit".</response>
    /// <response code="404">The customer has no credit account.</response>
    /// <response code="409">No open shift.</response>
    [HttpPost]
    [ProducesResponseType<CreditPaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterCreditPaymentCommand command, CancellationToken cancellationToken)
    {
        var voucher = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = voucher.Id }, voucher);
    }
}
