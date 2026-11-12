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
/// <param name="CustomerId">Required to take part of the sale on credit ("fiado"): the customer with a credit account.</param>
public sealed record RegisterSaleCommand(
    IReadOnlyList<RegisterSaleLine> Lines,
    IReadOnlyList<RegisterSalePayment> Payments,
    bool PrescriptionChecked,
    Guid? CustomerId = null) : IRequest<SaleDto>;

public sealed record RegisterSaleLine(Guid ProductId, Guid SaleUnitId, int Quantity);

/// <param name="Amount">For cash, what the customer handed over (the change is computed).</param>
public sealed record RegisterSalePayment(PaymentMethod Method, decimal Amount);

public sealed class RegisterSaleHandler(
    ICashSessionRepository sessions,
    IProductRepository products,
    IBatchRepository batches,
    ISaleRepository sales,
    ICreditAccountRepository creditAccounts,
    IDocumentNumberSequence documentNumbers,
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

        // The customer's credit account, tracked: if another register charges it at the same time, its RowVersion
        // makes the second sale a 409 instead of letting both pass the limit. The domain checks the limit.
        var creditAccount = command.CustomerId is { } customerId
            ? await creditAccounts.GetByCustomerAsync(pharmacyId, customerId, cancellationToken)
                ?? throw new ValidationException("El cliente no tiene fiado habilitado.")
            : null;

        // The ticket number and the sale commit together. The number's row stays locked until the commit (a
        // simultaneous sale waits instead of repeating it); if anything below fails, the rollback gives it back.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var number = await documentNumbers.NextAsync(pharmacyId, NumberedDocument.Sale, cancellationToken);
        var sale = Sale.Register(
            pharmacyId, number, session.Id, currentUser.UserId, clock.Now, lines, payments, command.PrescriptionChecked,
            creditAccount);
        sales.Add(sale);

        // Sale, batches with their kardex movements and the credit account with its entry, in one SaveChanges. Two
        // sales taking the last units of a batch (or the last of a customer's limit) at once: the second fails here
        // with a 409 (RowVersion on the batch or the account).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await reader.ToDtoAsync(sale, cancellationToken);
    }
}
