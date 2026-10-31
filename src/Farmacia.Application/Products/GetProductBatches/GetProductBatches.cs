using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Persistence;
using MediatR;

namespace Farmacia.Application.Products.GetProductBatches;

/// <summary>A product's batches with units, the first to expire first, with their cost (owner and stock keeper only).</summary>
public sealed record GetProductBatchesQuery(Guid ProductId) : IRequest<IReadOnlyList<BatchDto>>;

/// <param name="Quantity">In base units.</param>
/// <param name="UnitCost">Per base unit.</param>
/// <param name="IsExpired">Expired on the pharmacy's date: it cannot be sold, only written off.</param>
public sealed record BatchDto(
    Guid Id, string LotNumber, DateOnly ExpirationDate, int Quantity, decimal UnitCost, DateTimeOffset ReceivedAt, bool IsExpired);

public sealed class GetProductBatchesHandler(
    IProductRepository products,
    IBatchRepository batches,
    ICurrentPharmacy currentPharmacy,
    IClock clock) : IRequestHandler<GetProductBatchesQuery, IReadOnlyList<BatchDto>>
{
    public async Task<IReadOnlyList<BatchDto>> Handle(GetProductBatchesQuery query, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var product = await products.GetOrThrowAsync(pharmacyId, query.ProductId, cancellationToken);
        var today = DateOnly.FromDateTime(clock.Now.DateTime);

        return (await batches.ListInStockByProductAsync(pharmacyId, product.Id, cancellationToken))
            .Select(b => new BatchDto(b.Id, b.LotNumber, b.ExpirationDate, b.Quantity, b.UnitCost, b.ReceivedAt, b.IsExpiredOn(today)))
            .ToList();
    }
}
