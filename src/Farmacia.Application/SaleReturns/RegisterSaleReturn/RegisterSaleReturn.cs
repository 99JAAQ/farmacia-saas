using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Cash;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Sales;
using MediatR;

namespace Farmacia.Application.SaleReturns.RegisterSaleReturn;

/// <summary>A customer returns part or all of a ticket and gets the money back in cash.</summary>
public sealed record RegisterSaleReturnCommand(Guid SaleId, string Reason, IReadOnlyList<RegisterSaleReturnLine> Lines)
    : IRequest<SaleReturnDto>;

/// <param name="Quantity">In the sale unit of the ticket line (e.g. 1 of the 3 boxes).</param>
public sealed record RegisterSaleReturnLine(Guid SaleItemId, int Quantity, ReturnDisposition Disposition);

public sealed class RegisterSaleReturnHandler(
    ICashSessionRepository sessions,
    ISaleRepository sales,
    ISaleReturnRepository returns,
    IBatchRepository batches,
    IDocumentNumberSequence documentNumbers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock,
    SaleReturnReader reader) : IRequestHandler<RegisterSaleReturnCommand, SaleReturnDto>
{
    public async Task<SaleReturnDto> Handle(RegisterSaleReturnCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        if (command.Lines is not { Count: > 0 })
            throw new ValidationException("La devolución no tiene productos.");

        // The cash leaves the drawer of whoever makes the return, so it needs their own open shift.
        var session = await sessions.GetOpenOrThrowAsync(pharmacyId, cancellationToken);
        if (session.OpenedBy != currentUser.UserId)
            throw new ForbiddenException("La caja está abierta por otro cajero: solo esa persona puede hacer devoluciones en este turno.");

        var sale = await sales.GetByIdAsync(pharmacyId, command.SaleId, cancellationToken)
            ?? throw new NotFoundException("La venta no existe.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The number first: its row stays locked until the commit, so a simultaneous return waits here and, when it
        // goes on, reads this one among the earlier returns. Two returns can never both see "2 left" and return 4.
        var number = await documentNumbers.NextAsync(pharmacyId, NumberedDocument.SaleReturn, cancellationToken);
        var previous = await returns.ListBySaleAsync(pharmacyId, sale.Id, cancellationToken);

        var restocked = command.Lines
            .Where(l => l.Disposition == ReturnDisposition.Restock)
            .Select(l => l.SaleItemId)
            .ToHashSet();
        var batchIds = sale.Items
            .Where(i => restocked.Contains(i.Id))
            .SelectMany(i => i.Batches.Select(b => b.BatchId))
            .Distinct()
            .ToList();
        var saleBatches = batchIds.Count == 0 ? [] : await batches.GetByIdsAsync(pharmacyId, batchIds, cancellationToken);

        var saleReturn = SaleReturn.Register(
            number, sale, previous, session.Id, currentUser.UserId, clock.Now, command.Reason,
            command.Lines.Select(l => new ReturnLine(l.SaleItemId, l.Quantity, l.Disposition)).ToList(),
            saleBatches);
        returns.Add(saleReturn);

        // Return, batches and their kardex movements in one SaveChanges.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await reader.ToDtoAsync(saleReturn, sale, cancellationToken);
    }
}
