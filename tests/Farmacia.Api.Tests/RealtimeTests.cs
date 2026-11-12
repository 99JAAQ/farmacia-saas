using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farmacia.Application.Authorization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Farmacia.Api.Tests;

/// <summary>The real-time hub, with a real SignalR client against the API in memory.</summary>
[Collection(ApiCollection.Name)]
public class RealtimeTests(ApiFactory api)
{
    /// <summary>How long a test waits for a notice that must NOT arrive.</summary>
    private static readonly TimeSpan Silence = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ASale_IsAnnouncedToItsPharmacysScreens_AndNeverToAnotherPharmacy()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var stranger = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        await using var ownerScreen = await Listen(owner);
        await using var strangerScreen = await Listen(stranger);

        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 90);
        await OpenShiftAsync(cashier);
        var sale = await cashier.PostAsJsonAsync("/api/sales", Cart(productId, boxId, boxes: 1));
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
        var saleId = (await sale.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var changes = await ownerScreen.NextAsync();
        Assert.Contains(new Change("sales", saleId), changes);
        Assert.Contains(new Change("stock", productId), changes);
        await strangerScreen.ExpectSilenceAsync();
    }

    [Fact]
    public async Task AFailedSale_AnnouncesNothing()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var cashier = await api.LoggedInClientAsync(pharmacyId, Roles.Cashier);
        await using var screen = await Listen(cashier);
        var (productId, boxId) = await api.SeedProductWithStockAsync(pharmacyId, tablets: 30);
        await OpenShiftAsync(cashier);

        var tooMany = await cashier.PostAsJsonAsync("/api/sales", Cart(productId, boxId, boxes: 2));

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        await screen.ExpectSilenceAsync();
    }

    [Fact]
    public async Task OnlyAPharmacysUsers_CanListen()
    {
        var pharmacyId = await api.CreatePharmacyAsync();
        var temporary = await api.LoggedInAsAsync(await CreateUserWithTemporaryPasswordAsync(pharmacyId), "Temporal2026");

        await AssertRefusedAsync(null, HttpStatusCode.Unauthorized);
        await AssertRefusedAsync(await api.LoggedInPlatformAdminAsync(), HttpStatusCode.Forbidden); // no pharmacy
        await AssertRefusedAsync(temporary, HttpStatusCode.Forbidden); // must change the password first
    }

    [Fact]
    public async Task TheTokenInTheQueryString_IsAcceptedOnlyByTheHub()
    {
        // Browsers cannot send headers when opening a WebSocket: SignalR puts the token in the URL. Only the hub reads it.
        var owner = await api.LoggedInClientAsync(await api.CreatePharmacyAsync(), Roles.Owner);
        var token = owner.DefaultRequestHeaders.Authorization!.Parameter;
        var anonymous = api.CreateHttpsClient();

        var hub = await anonymous.PostAsync($"/hubs/pharmacy/negotiate?negotiateVersion=1&access_token={token}", null);
        var rest = await anonymous.GetAsync($"/api/pharmacy?access_token={token}");

        Assert.Equal(HttpStatusCode.OK, hub.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rest.StatusCode);
    }

    private async Task AssertRefusedAsync(HttpClient? client, HttpStatusCode expected)
    {
        var connection = Connection(client);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.Equal(expected, error.StatusCode);
    }

    private async Task<string> CreateUserWithTemporaryPasswordAsync(Guid pharmacyId)
    {
        var owner = await api.LoggedInClientAsync(pharmacyId, Roles.Owner);
        var email = $"temporal-{Guid.CreateVersion7():N}@farmacia.test";
        var created = await owner.PostAsJsonAsync("/api/users", new { fullName = "Temporal", email, role = Roles.Cashier, temporaryPassword = "Temporal2026" });
        created.EnsureSuccessStatusCode();
        return email;
    }

    private async Task<Screen> Listen(HttpClient client)
    {
        var connection = Connection(client);
        var screen = new Screen(connection);
        await connection.StartAsync();
        return screen;
    }

    /// <summary>Long polling over the in-memory server; the token goes in the Authorization header, as for REST.</summary>
    private HubConnection Connection(HttpClient? client)
    {
        var token = client?.DefaultRequestHeaders.Authorization?.Parameter;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "hubs/pharmacy"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private static async Task OpenShiftAsync(HttpClient client) =>
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/cash-sessions", new { openingFloat = 100_000 })).StatusCode);

    private static object Cart(Guid productId, Guid boxId, int boxes) => new
    {
        lines = new[] { new { productId, saleUnitId = boxId, quantity = boxes } },
        payments = new[] { new { method = "Cash", amount = 50_000 } },
        prescriptionChecked = false,
    };

    private sealed record IdDto(Guid Id);

    public sealed record Change(string Topic, Guid? Id);

    /// <summary>An open screen: collects the notices it receives.</summary>
    private sealed class Screen : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly BlockingCollection<Change[]> _received = [];

        public Screen(HubConnection connection)
        {
            _connection = connection;
            connection.On<Change[]>("changed", changes => _received.Add(changes));
        }

        public Task<Change[]> NextAsync() => Task.Run(() =>
            _received.TryTake(out var changes, TimeSpan.FromSeconds(10)) ? changes : throw new TimeoutException("No notice arrived."));

        public async Task ExpectSilenceAsync()
        {
            await Task.Delay(Silence);
            Assert.Empty(_received);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
