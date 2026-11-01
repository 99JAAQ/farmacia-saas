using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Products;
using Farmacia.Domain.Inventory;
using MediatR;

namespace Farmacia.Application.Inventory.GetProductKardex;

/// <summary>A product's latest stock movements, newest first, with what the product had after each one.</summary>
public sealed record GetProductKardexQuery(Guid ProductId) : IRequest<IReadOnlyList<KardexEntryDto>>;

/// <param name="QuantityChange">In base units: positive in, negative out.</param>
/// <param name="ProductBalanceAfter">All the product's units right after the movement, in every batch.</param>
/// <param name="BatchBalanceAfter">The batch's units right after the movement.</param>
public sealed record KardexEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    StockMovementType Type,
    string LotNumber,
    int QuantityChange,
    int ProductBalanceAfter,
    int BatchBalanceAfter,
    string? Document,
    string? Notes,
    string? PerformedByName);

public sealed class GetProductKardexHandler(
    IProductRepository products,
    IKardexReader kardex,
    ICurrentPharmacy currentPharmacy) : IRequestHandler<GetProductKardexQuery, IReadOnlyList<KardexEntryDto>>
{
    // Enough to explain today's stock at a glance; a full history report comes with the reports.
    private const int MaxEntries = 200;

    public async Task<IReadOnlyList<KardexEntryDto>> Handle(GetProductKardexQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var product = await products.GetOrThrowAsync(pharmacyId, query.ProductId, cancellationToken);

        var rows = await kardex.ListByProductAsync(pharmacyId, product.Id, MaxEntries, cancellationToken);

        // The kardex stores each batch's balance; the product's comes backwards from today's stock: the newest movement
        // left today's units, and each older one left those minus the change that came after it. It holds because
        // every stock change goes through the kardex.
        var balance = await kardex.GetProductUnitsAsync(pharmacyId, product.Id, cancellationToken);
        var entries = new List<KardexEntryDto>(rows.Count);
        foreach (var row in rows)
        {
            entries.Add(new KardexEntryDto(
                row.Id, row.OccurredAt, row.Type, row.LotNumber, row.QuantityChange, balance, row.BatchBalanceAfter,
                row.Document, row.Notes, row.PerformedByName));
            balance -= row.QuantityChange;
        }

        return entries;
    }
}
