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
}

/// <summary>A thin notice: a topic and, when it helps the browser refresh less, the Id of what changed.</summary>
public sealed record RealtimeChange(string Topic, Guid? Id = null);

/// <summary>
/// Scoped: the use case adds what it changed; the notices go out only after it finishes successfully, that is,
/// after its transaction committed (PublishRealtimeChangesBehavior). A use case that fails sends nothing.
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
