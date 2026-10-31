using Farmacia.Api.Authentication;
using Farmacia.Application.Credit;
using Farmacia.Application.Credit.ChangeCreditLimit;
using Farmacia.Application.Credit.GetCreditStatement;
using Farmacia.Application.Credit.ListDebtors;
using Farmacia.Application.Credit.OpenCreditAccount;
using Farmacia.Application.Credit.SetCreditSuspended;
using Farmacia.Application.Customers;
using Farmacia.Application.Customers.CreateCustomer;
using Farmacia.Application.Customers.GetCustomerById;
using Farmacia.Application.Customers.SearchCustomers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// Customers and their credit ("fiado"). Cashiers find customers and see what they owe (to sell on credit and to tell
/// them their balance); only the owner registers customers and decides who gets credit and how much (ManageCredit,
/// required on top of OperateCash).
/// </summary>
[ApiController]
[Route("api/customers")]
[Authorize(Policy = Policies.OperateCash)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    /// <summary>By name (ignoring case and accents) or by exact document number, with their credit.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SearchCustomersQuery(search), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetCustomerByIdQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageCredit)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }

    /// <summary>Who owes, the oldest debt first.</summary>
    [HttpGet("debtors")]
    [Authorize(Policy = Policies.ManageCredit)]
    [ProducesResponseType<IReadOnlyList<DebtorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Debtors(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListDebtorsQuery(), cancellationToken));

    /// <summary>The customer's balance and latest movements, newest first.</summary>
    [HttpGet("{id:guid}/credit")]
    [ProducesResponseType<CreditStatementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreditStatementDto>> Statement(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetCreditStatementQuery(id), cancellationToken);

    /// <summary>Enables credit for the customer, with a limit.</summary>
    [HttpPost("{id:guid}/credit")]
    [Authorize(Policy = Policies.ManageCredit)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpenCredit(Guid id, CreditLimitRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new OpenCreditAccountCommand(id, request.Limit), cancellationToken);
        return CreatedAtAction(nameof(Statement), new { id }, null);
    }

    /// <summary>Below the current debt, it only stops new sales on credit.</summary>
    [HttpPut("{id:guid}/credit/limit")]
    [Authorize(Policy = Policies.ManageCredit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeLimit(Guid id, CreditLimitRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ChangeCreditLimitCommand(id, request.Limit), cancellationToken);
        return NoContent();
    }

    /// <summary>No new sales on credit; payments are still taken.</summary>
    [HttpPost("{id:guid}/credit/suspend")]
    [Authorize(Policy = Policies.ManageCredit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SetCreditSuspendedCommand(id, Suspended: true), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/credit/resume")]
    [Authorize(Policy = Policies.ManageCredit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new SetCreditSuspendedCommand(id, Suspended: false), cancellationToken);
        return NoContent();
    }
}

public sealed record CreditLimitRequest(decimal Limit);
