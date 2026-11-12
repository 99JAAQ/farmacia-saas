using Farmacia.Application.Abstractions.Realtime;
using Farmacia.Domain.Cash;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;
using Farmacia.Domain.Inventory;
using Farmacia.Domain.Pharmacies;
using Farmacia.Domain.Products;
using Farmacia.Domain.Purchases;
using Farmacia.Domain.Sales;
using Farmacia.Domain.Suppliers;
using Farmacia.Infrastructure.Identity;
using Farmacia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Farmacia.Infrastructure.Realtime;

/// <summary>
/// Which notice each saved entity produces. Every entity of the model must be here, even if only to say "nothing"
/// (its parent already speaks for it): RealtimeChangeMapTests fails when a new entity is left out, so no screen is
/// left stale because someone forgot to announce a change.
/// </summary>
public static class RealtimeChangeMap
{
    private static readonly Func<object, RealtimeChange?> Nothing = _ => null;

    private static readonly Dictionary<Type, Func<object, RealtimeChange?>> Map = new()
    {
        [typeof(Pharmacy)] = _ => new(RealtimeTopics.Pharmacy),

        // Catalog
        [typeof(Category)] = _ => new(RealtimeTopics.Catalog),
        [typeof(Product)] = e => new(RealtimeTopics.Catalog, ((Product)e).Id),
        [typeof(SaleUnit)] = e => new(RealtimeTopics.Catalog, ((SaleUnit)e).ProductId),
        [typeof(Supplier)] = e => new(RealtimeTopics.Suppliers, ((Supplier)e).Id),
        [typeof(Customer)] = e => new(RealtimeTopics.Customers, ((Customer)e).Id),

        // Stock: the batch carries the quantity; its kardex movements are saved with it.
        [typeof(Batch)] = e => new(RealtimeTopics.Stock, ((Batch)e).ProductId),
        [typeof(StockMovement)] = Nothing,

        // Purchases and what is owed to suppliers
        [typeof(Purchase)] = e => new(RealtimeTopics.Purchases, ((Purchase)e).Id),
        [typeof(PurchaseItem)] = e => new(RealtimeTopics.Purchases, ((PurchaseItem)e).PurchaseId),
        [typeof(SupplierPayment)] = e => new(RealtimeTopics.Purchases, ((SupplierPayment)e).PurchaseId),

        // The register
        [typeof(CashSession)] = e => new(RealtimeTopics.Cash, ((CashSession)e).Id),
        [typeof(CashMovement)] = e => new(RealtimeTopics.Cash, ((CashMovement)e).CashSessionId),

        // Sales and returns (the shift's cash changes with them: the browser refreshes both)
        [typeof(Sale)] = e => new(RealtimeTopics.Sales, ((Sale)e).Id),
        [typeof(SaleItem)] = Nothing,
        [typeof(SaleItemBatch)] = Nothing,
        [typeof(Payment)] = Nothing,
        [typeof(SaleReturn)] = e => new(RealtimeTopics.Sales, ((SaleReturn)e).SaleId),
        [typeof(SaleReturnItem)] = Nothing,
        [typeof(SaleReturnItemBatch)] = Nothing,

        // Credit ("fiado"): by customer, so the customer's page refreshes
        [typeof(CreditAccount)] = e => new(RealtimeTopics.Credit, ((CreditAccount)e).CustomerId),
        [typeof(CreditEntry)] = Nothing,
        [typeof(CreditPayment)] = e => new(RealtimeTopics.Credit, ((CreditPayment)e).CustomerId),

        // Users
        [typeof(ApplicationUser)] = e => new(RealtimeTopics.Users, ((ApplicationUser)e).Id),
        [typeof(IdentityUserRole<Guid>)] = e => new(RealtimeTopics.Users, ((IdentityUserRole<Guid>)e).UserId),

        // Internal bookkeeping: nobody looks at it on a screen.
        [typeof(DocumentCounter)] = Nothing,
        [typeof(RefreshToken)] = Nothing,
        [typeof(IdentityRole<Guid>)] = Nothing,
        [typeof(IdentityRoleClaim<Guid>)] = Nothing,
        [typeof(IdentityUserClaim<Guid>)] = Nothing,
        [typeof(IdentityUserLogin<Guid>)] = Nothing,
        [typeof(IdentityUserToken<Guid>)] = Nothing,
    };

    public static bool Covers(Type entityType) => Map.ContainsKey(entityType);

    public static RealtimeChange? For(object entity) =>
        Map.TryGetValue(entity.GetType(), out var notice) ? notice(entity) : null;
}
