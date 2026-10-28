using Farmacia.Api.Authentication;
using Farmacia.Application.Cash;
using Farmacia.Application.Cash.CloseCashSession;
using Farmacia.Application.Cash.GetCashSessionById;
using Farmacia.Application.Cash.GetCurrentCashSession;
using Farmacia.Application.Cash.OpenCashSession;
using Farmacia.Application.Cash.RecordCashMovement;
using Farmacia.Domain.Cash;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// The cash register's shifts. One register: at most one open shift, and only whoever opened it touches its cash.
/// The cashier who opens and the time come from the token and the server clock, never from the body.
/// </summary>
[ApiController]
[Route("api/cash-sessions")]
[Authorize(Policy = Policies.OperateCash)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CashSessionsController(ISender sender) : ControllerBase
{
    /// <summary>The open shift, or null and the float to suggest for the next one.</summary>
    [HttpGet("current")]
    [ProducesResponseType<CurrentCashSessionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentCashSessionDto>> GetCurrent(CancellationToken cancellationToken) =>
        await sender.Send(new GetCurrentCashSessionQuery(), cancellationToken);

    /// <summary>While open, the expected cash is shown only to the owner (blind count).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CashSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashSessionDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetCashSessionByIdQuery(id), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Open(OpenCashSessionRequest request, CancellationToken cancellationToken)
    {
        var id = await sender.Send(new OpenCashSessionCommand(request.OpeningFloat), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }

    /// <summary>Cash in or out that is not a sale, in the current user's open shift.</summary>
    [HttpPost("current/movements")]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordMovement(CashMovementRequest request, CancellationToken cancellationToken)
    {
        var movementId = await sender.Send(
            new RecordCashMovementCommand(request.Type, request.Amount, request.Reason), cancellationToken);

        // The movement has no URL of its own: Location points to the shift that now includes it.
        return Created("/api/cash-sessions/current", new CreatedResponse(movementId));
    }

    /// <summary>Closes the shift with the cash counted by hand; the answer reveals the expected cash and the difference.</summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType<CashSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashSessionDto>> Close(
        Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken) =>
        await sender.Send(new CloseCashSessionCommand(id, request.CountedCash, request.Notes), cancellationToken);
}

public sealed record OpenCashSessionRequest(decimal OpeningFloat);

public sealed record CashMovementRequest(CashMovementType Type, decimal Amount, string Reason);

public sealed record CloseCashSessionRequest(decimal CountedCash, string? Notes);
