using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Products;

namespace Farmacia.Application.Products;

internal static class ProductRepositoryExtensions
{
    /// <summary>A product of another pharmacy is not found here either: 404, never 403.</summary>
    public static async Task<Product> GetOrThrowAsync(
        this IProductRepository products, Guid pharmacyId, Guid productId, CancellationToken cancellationToken) =>
        await products.GetByIdAsync(pharmacyId, productId, cancellationToken)
            ?? throw new NotFoundException("El producto no existe.");

    /// <summary>
    /// One barcode, one presentation in the whole pharmacy. The product checks its own units; this checks the others,
    /// with a clear message (the unique index is the last line of defense).
    /// </summary>
    public static async Task EnsureBarcodeFreeAsync(
        this IProductRepository products, Product product, string? barcode, CancellationToken cancellationToken)
    {
        var code = barcode?.Trim();
        if (string.IsNullOrEmpty(code)) return;

        // Already one of this product's codes: the same unit keeps it (fine), another unit takes it (the domain rejects it).
        if (product.SaleUnits.Any(u => u.Barcode == code)) return;

        if ((await products.GetExistingBarcodesAsync(product.PharmacyId, [code], cancellationToken)).Count > 0)
            throw new ConflictException($"El código de barras {code} ya está asignado a otro producto.");
    }
}
