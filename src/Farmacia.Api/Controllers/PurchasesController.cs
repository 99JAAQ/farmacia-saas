using Farmacia.Api.Authentication;
using Farmacia.Application.Purchases;
using Farmacia.Application.Purchases.AddPurchaseItem;
using Farmacia.Application.Purchases.ConfirmPurchase;
using Farmacia.Application.Purchases.CreatePurchaseDraft;
using Farmacia.Application.Purchases.GetPurchaseById;
using Farmacia.Application.Purchases.ListPayables;
using Farmacia.Application.Purchases.ListPurchases;
using Farmacia.Application.Purchases.RegisterSupplierPayment;
using Farmacia.Application.Purchases.RemovePurchaseItem;
using Farmacia.Application.Purchases.SetPurchaseInvoiceDiscount;
using Farmacia.Application.Purchases.UpdatePurchaseHeader;
using Farmacia.Domain.Common;
using Farmacia.Domain.Purchases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// Supplier invoices: typed in as a draft (each action is saved at once, so a power cut loses nothing)
/// and confirmed when the goods are checked. Ids come from the URL, never from the body.
/// </summary>
[ApiController]
[Route("api/purchases")]
[Authorize(Policy = Policies.ReceivePurchases)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PurchasesController(ISender sender) : ControllerBase
{
    /// <summary>Most recent purchases. With status=Draft: the invoices left half-typed.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PurchaseSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PurchaseStatus? status, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListPurchasesQuery(status), cancellationToken));

    /// <summary>Credit invoices still owed, the most overdue first. The stock keeper sees them too; only the owner pays.</summary>
    [HttpGet("payables")]
    [ProducesResponseType<IReadOnlyList<PayableInvoiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Payables([FromQuery] Guid? supplierId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListPayablesQuery(supplierId), cancellationToken));

    /// <summary>Pays part or all of a credit invoice, outside the register. Owner only (on top of ReceivePurchases).</summary>
    /// <response code="400">Over the balance, a cash purchase, a draft, or a transfer or check without its number.</response>
    /// <response code="409">Another payment on the same invoice was saved at the same time.</response>
    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = Policies.PaySuppliers)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(Guid id, SupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        var paymentId = await sender.Send(
            new RegisterSupplierPaymentCommand(id, request.Amount, request.Method, request.Reference), cancellationToken);

        // The payment has no URL of its own: Location points to the invoice, which now shows it and the new balance.
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(paymentId));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PurchaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        await sender.Send(new GetPurchaseByIdQuery(id), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreatePurchaseDraftCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(id));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateHeader(Guid id, PurchaseHeaderRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdatePurchaseHeaderCommand(
            id, request.SupplierId, request.InvoiceNumber, request.InvoiceDate, request.PaymentTerm, request.DueDate),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid id, AddPurchaseItemRequest request, CancellationToken cancellationToken)
    {
        var itemId = await sender.Send(new AddPurchaseItemCommand(
            id, request.ProductId, request.SaleUnitId, request.Quantity, request.BonusQuantity, request.UnitCost,
            request.LineDiscount, request.VatRate, request.LotNumber, request.ExpirationDate),
            cancellationToken);

        // The line has no URL of its own: Location points to the purchase that now includes it.
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedResponse(itemId));
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemovePurchaseItemCommand(id, itemId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/invoice-discount")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetInvoiceDiscount(
        Guid id, InvoiceDiscountRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPurchaseInvoiceDiscountCommand(id, request.Discount), cancellationToken);
        return NoContent();
    }

    /// <summary>Receives the goods: the batches enter the inventory and the purchase can no longer be edited.</summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<PurchaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseDto>> Confirm(
        Guid id, ConfirmPurchaseRequest request, CancellationToken cancellationToken) =>
        await sender.Send(new ConfirmPurchaseCommand(id, request.PrintedInvoiceTotal), cancellationToken);
}

public sealed record PurchaseHeaderRequest(
    Guid SupplierId, string InvoiceNumber, DateOnly InvoiceDate, PaymentTerm PaymentTerm, DateOnly? DueDate);

public sealed record AddPurchaseItemRequest(
    Guid ProductId,
    Guid SaleUnitId,
    int Quantity,
    int BonusQuantity,
    decimal UnitCost,
    decimal LineDiscount,
    VatRate VatRate,
    string LotNumber,
    DateOnly ExpirationDate);

public sealed record InvoiceDiscountRequest(decimal Discount);

/// <param name="PrintedInvoiceTotal">The total printed on the paper invoice.</param>
public sealed record ConfirmPurchaseRequest(decimal PrintedInvoiceTotal);

/// <param name="Reference">The transfer or check number (required for those two methods).</param>
public sealed record SupplierPaymentRequest(decimal Amount, SupplierPaymentMethod Method, string? Reference);
