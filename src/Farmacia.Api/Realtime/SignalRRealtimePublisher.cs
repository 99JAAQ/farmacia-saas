using Farmacia.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Farmacia.Api.Realtime;

/// <summary>Sends a use case's notices to every open screen of its pharmacy, and to no other pharmacy.</summary>
internal sealed class SignalRRealtimePublisher(IHubContext<PharmacyHub> hub) : IRealtimePublisher
{
    public Task PublishAsync(Guid pharmacyId, IReadOnlyList<RealtimeChange> changes, CancellationToken cancellationToken) =>
        hub.Clients.Group(PharmacyHub.GroupFor(pharmacyId)).SendAsync(PharmacyHub.ChangedMethod, changes, cancellationToken);
}
