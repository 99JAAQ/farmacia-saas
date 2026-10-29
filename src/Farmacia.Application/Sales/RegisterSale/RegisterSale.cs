using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Cash;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Sales;
using MediatR;

namespace Farmacia.Application.Sales.RegisterSale;

/// <summary>
/// "Cobrar": the whole cart at once. Prices are never sent: they come from the catalog.
/// </summary>
/// <param name="PrescriptionChecked">The cashier ticked "Fórmula revisada" (required if a product needs one).</param>
public sealed record RegisterSaleCommand(
    IReadOnlyList<RegisterSaleLine> Lines,
    IReadOnlyList<RegisterSalePayment> Payments,
    bool PrescriptionChecked) : IRequest<SaleDto>;

public sealed record RegisterSaleLine(Guid ProductId, Guid SaleUnitId, int Quantity);

/// <param name="Amount">For cash, what the customer handed over (the change is computed).</param>
public sealed record RegisterSalePayment(PaymentMethod Method, decimal Amount);

public sealed class RegisterSaleHandler(
    ICashSessionRepository sessions,
    IProductRepository products,
    IBatchRepository batches,
    ISaleRepository sales,
    ITicketNumberSequence ticketNumbers,
    IUnitOfWork unitOfWork,
    ICurrentPharmacy currentPharmacy,
    ICurrentUser currentUser,
    IClock clock,
    SaleReader reader) : IRequestHandler<RegisterSaleCommand, SaleDto>
{
    public async Task<SaleDto> Handle(RegisterSaleCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;

        if (command.Lines is not { Count: > 0 })
            throw new ValidationException("La venta no tiene productos.");

        // Sales belong to a shift, and only whoever opened it sells in it: the cash must have one owner.
        var session = await sessions.GetOpenOrThrowAsync(pharmacyId, cancellationToken);
        if (session.OpenedBy != currentUser.UserId)
            throw new ForbiddenException("La caja está abierta por otro cajero: solo esa persona puede vender en este turno.");

        var productIds = command.Lines.Select(l => l.ProductId).Distinct().ToList();
        var catalog = (await products.GetByIdsAsync(pharmacyId, productIds, cancellationToken)).ToDictionary(p => p.Id);
        if (catalog.Count != productIds.Count)
            throw new ValidationException("Un producto de la venta no existe.");

        var stock = await batches.GetInStockAsync(pharmacyId, productIds, cancellationToken);
        var lines = command.Lines
            .Select(l => new SaleLine(catalog[l.ProductId], l.SaleUnitId, l.Quantity, stock.Where(b => b.ProductId == l.ProductId).ToList()))
            .ToList();
        var payments = (command.Payments ?? []).Select(p => new PaymentLine(p.Method, p.Amount)).ToList();

        // The ticket number and the sale commit together. The number's row stays locked until the commit (a
        // simultaneous sale waits instead of repeating it); if anything below fails, the rollback gives it back.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var number = await ticketNumbers.NextAsync(pharmacyId, cancellationToken);
        var sale = Sale.Register(
            pharmacyId, number, session.Id, currentUser.UserId, clock.Now, lines, payments, command.PrescriptionChecked);
        sales.Add(sale);

        // Sale, batches and their kardex movements in one SaveChanges. Two sales taking the last units of the same
        // batch at once: the second fails here with a 409 (RowVersion on the batch).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await reader.ToDtoAsync(sale, cancellationToken);
    }
}
