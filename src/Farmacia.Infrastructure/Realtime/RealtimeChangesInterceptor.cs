using Farmacia.Application.Abstractions.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Farmacia.Infrastructure.Realtime;

/// <summary>
/// Announces what each SaveChanges actually wrote, so no use case has to remember to. It only records the notices
/// in the request's buffer: PublishRealtimeChangesBehavior sends them once the use case returns (after its commit),
/// and drops them if it fails afterwards (e.g. its transaction rolls back).
/// </summary>
/// <remarks>One instance per DbContext (it is created with the scope's context), so the field is not shared.</remarks>
internal sealed class RealtimeChangesInterceptor(Func<IRealtimeNotifications> notifications) : SaveChangesInterceptor
{
    private List<RealtimeChange>? _pending;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Record();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Record();
        return ValueTask.FromResult(result);
    }

    // A failed save wrote nothing: nothing to announce.
    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending = null;

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = null;
        return Task.CompletedTask;
    }

    /// <summary>Before saving: afterwards the entries are no longer Added or Modified.</summary>
    private void Capture(DbContext? context) =>
        _pending = context?.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => RealtimeChangeMap.For(e.Entity))
            .OfType<RealtimeChange>()
            .ToList();

    private void Record()
    {
        if (_pending is null)
            return;

        var buffer = notifications();
        foreach (var change in _pending)
            buffer.Add(change.Topic, change.Id);
        _pending = null;
    }
}
