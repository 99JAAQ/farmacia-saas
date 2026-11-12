using Farmacia.Application.Abstractions;
using Farmacia.Application.Abstractions.Realtime;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farmacia.Application.Common.Realtime;

/// <summary>The notices of one request, held until its use case succeeds. Repeated notices are sent once.</summary>
public sealed class RealtimeNotifications : IRealtimeNotifications
{
    private readonly List<RealtimeChange> _changes = [];

    public void Add(string topic, Guid? id = null)
    {
        var change = new RealtimeChange(topic, id);
        if (!_changes.Contains(change))
            _changes.Add(change);
    }

    /// <summary>Hands the pending notices over and forgets them.</summary>
    internal IReadOnlyList<RealtimeChange> Drain()
    {
        var changes = _changes.ToList();
        _changes.Clear();
        return changes;
    }
}

/// <summary>
/// Around every use case: if it returns, its transaction committed, so its notices go out; if it throws, they are
/// dropped. Best effort: a notice that cannot be delivered never fails a sale that is already saved; the browser
/// fetches everything again when it reconnects. Real time is a convenience, the database is the truth.
/// </summary>
public sealed class PublishRealtimeChangesBehavior<TRequest, TResponse>(
    RealtimeNotifications notifications,
    IRealtimePublisher publisher,
    ICurrentPharmacy currentPharmacy,
    ILogger<PublishRealtimeChangesBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch
        {
            notifications.Drain(); // nothing was saved: nothing to announce
            throw;
        }

        var changes = notifications.Drain();
        if (changes.Count == 0 || currentPharmacy.PharmacyIdOrNull is not { } pharmacyId)
            return response;

        try
        {
            // Not the request's token: the sale is saved, so a client that hangs up must not cancel the notice.
            await publisher.PublishAsync(pharmacyId, changes, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not publish {Count} real-time changes for {Request}", changes.Count, typeof(TRequest).Name);
        }

        return response;
    }
}

/// <summary>Where nothing listens (tests of the use cases, tools): the notices go nowhere.</summary>
internal sealed class NoRealtimePublisher : IRealtimePublisher
{
    public Task PublishAsync(Guid pharmacyId, IReadOnlyList<RealtimeChange> changes, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
