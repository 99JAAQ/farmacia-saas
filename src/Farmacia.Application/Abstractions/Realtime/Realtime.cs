namespace Farmacia.Application.Abstractions.Realtime;

/// <summary>
/// What changed, in a word. The browser maps each topic to the queries it must fetch again: the notice carries no
/// data, so permissions stay where they are (the REST endpoints), and a cashier never receives a cost.
/// </summary>
public static class RealtimeTopics
{
    /// <summary>A sale or a return: the day's totals and the shift's sales.</summary>
    public const string Sales = "sales";

    /// <summary>The stock of a product changed (sale, return, purchase, write-off, adjustment). Id = the product.</summary>
    public const string Stock = "stock";

    /// <summary>A product, its presentations or the categories. Id = the product, when there is one.</summary>
    public const string Catalog = "catalog";

    /// <summary>A purchase, its lines or a payment to the supplier. Id = the purchase.</summary>
    public const string Purchases = "purchases";

    /// <summary>A cash shift or one of its movements. Id = the shift.</summary>
    public const string Cash = "cash";

    /// <summary>A customer's credit account or a payment to it. Id = the customer.</summary>
    public const string Credit = "credit";

    public const string Customers = "customers";

    public const string Suppliers = "suppliers";

    /// <summary>The pharmacy's own data (name, NIT, address on the tickets).</summary>
    public const string Pharmacy = "pharmacy";

    /// <summary>The pharmacy's users. Id = the user.</summary>
    public const string Users = "users";
}

/// <summary>A thin notice: a topic and, when it helps the browser refresh less, the Id of what changed.</summary>
public sealed record RealtimeChange(string Topic, Guid? Id = null);

/// <summary>
/// Scoped: what the request changed. Infrastructure fills it from every successful SaveChanges (so no use case has
/// to remember); the notices go out only after the use case finishes successfully, that is, after its transaction
/// committed (PublishRealtimeChangesBehavior). A use case that fails sends nothing.
/// </summary>
public interface IRealtimeNotifications
{
    void Add(string topic, Guid? id = null);
}

/// <summary>Delivers the notices to the pharmacy's open screens (SignalR, in the API).</summary>
public interface IRealtimePublisher
{
    Task PublishAsync(Guid pharmacyId, IReadOnlyList<RealtimeChange> changes, CancellationToken cancellationToken);
}
