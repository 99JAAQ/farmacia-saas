using Farmacia.Api.Authentication;
using Farmacia.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Farmacia.Api.Realtime;

/// <summary>
/// One connection per open browser tab. Each connection joins the group of its pharmacy, taken from the signed token
/// (never from the client), so a pharmacy only ever hears about its own changes. The hub has no methods the client
/// can call: it only pushes "changed" notices, which carry no data.
/// </summary>
[Authorize(Policy = Policies.UsePharmacy)]
public sealed class PharmacyHub : Hub
{
    public const string Path = "/hubs/pharmacy";

    /// <summary>The client-side method that receives the notices.</summary>
    public const string ChangedMethod = "changed";

    public static string GroupFor(Guid pharmacyId) => $"pharmacy:{pharmacyId}";

    public override async Task OnConnectedAsync()
    {
        // The policy already required the claim; parsing again keeps a malformed one from joining a strange group.
        if (!Guid.TryParse(Context.User?.FindFirst(ClaimNames.PharmacyId)?.Value, out var pharmacyId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(pharmacyId));
        await base.OnConnectedAsync();
    }
}
