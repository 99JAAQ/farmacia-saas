using Farmacia.Application.Abstractions.Realtime;
using Farmacia.Application.Common.Realtime;
using Farmacia.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Farmacia.Application.Tests.Common;

public class PublishRealtimeChangesBehaviorTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();

    private readonly RealtimeNotifications _notifications = new();
    private readonly RecordingPublisher _publisher = new();

    private PublishRealtimeChangesBehavior<string, int> Behavior() =>
        new(_notifications, _publisher, new FakeCurrentPharmacy(PharmacyId), NullLogger<PublishRealtimeChangesBehavior<string, int>>.Instance);

    [Fact]
    public async Task AfterTheUseCaseSucceeds_ItsNoticesGoToItsPharmacy_Once()
    {
        var productId = Guid.CreateVersion7();

        await Behavior().Handle("venta", _ =>
        {
            _notifications.Add(RealtimeTopics.Sales);
            _notifications.Add(RealtimeTopics.Stock, productId);
            _notifications.Add(RealtimeTopics.Stock, productId); // two lines of the same product
            return Task.FromResult(1);
        }, CancellationToken.None);

        var (pharmacyId, changes) = Assert.Single(_publisher.Published);
        Assert.Equal(PharmacyId, pharmacyId);
        Assert.Equal([new RealtimeChange(RealtimeTopics.Sales), new RealtimeChange(RealtimeTopics.Stock, productId)], changes);
    }

    [Fact]
    public async Task WhenTheUseCaseFails_NothingIsAnnounced_NotEvenByTheNextOne()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Behavior().Handle("venta", _ =>
        {
            _notifications.Add(RealtimeTopics.Sales);
            throw new InvalidOperationException("sin stock");
        }, CancellationToken.None));

        await Behavior().Handle("consulta", _ => Task.FromResult(1), CancellationToken.None);

        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task ANoticeThatCannotBeDelivered_DoesNotFailWhatWasAlreadySaved()
    {
        _publisher.Fail = true;

        var result = await Behavior().Handle("venta", _ =>
        {
            _notifications.Add(RealtimeTopics.Sales);
            return Task.FromResult(42);
        }, CancellationToken.None);

        Assert.Equal(42, result);
    }

    private sealed class RecordingPublisher : IRealtimePublisher
    {
        public bool Fail { get; set; }
        public List<(Guid PharmacyId, IReadOnlyList<RealtimeChange> Changes)> Published { get; } = [];

        public Task PublishAsync(Guid pharmacyId, IReadOnlyList<RealtimeChange> changes, CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("SignalR caído");
            Published.Add((pharmacyId, changes));
            return Task.CompletedTask;
        }
    }
}
