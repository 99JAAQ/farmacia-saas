using Farmacia.Application.Cash;
using Farmacia.Application.Credit;
using Farmacia.Application.Purchases;
using Farmacia.Application.SaleReturns;
using Farmacia.Application.Sales;
using Farmacia.Application.Abstractions.Realtime;
using Farmacia.Application.Common.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Farmacia.Application;

public static class DependencyInjection
{
    /// <param name="mediatRLicenseKey">MediatR Community license key (free under USD 5M revenue). Without it MediatR still runs but logs a warning.</param>
    public static IServiceCollection AddApplication(this IServiceCollection services, string? mediatRLicenseKey)
    {
        services.AddMediatR(config =>
        {
            config.LicenseKey = mediatRLicenseKey;
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            config.AddOpenBehavior(typeof(PublishRealtimeChangesBehavior<,>));
        });

        // Real-time notices: held per request, published after the use case succeeds. The API replaces the
        // publisher with SignalR; elsewhere (tests, tools) they go nowhere.
        services.AddScoped<RealtimeNotifications>();
        services.AddScoped<IRealtimeNotifications>(provider => provider.GetRequiredService<RealtimeNotifications>());
        services.TryAddSingleton<IRealtimePublisher, NoRealtimePublisher>();

        services.AddScoped<PurchaseReader>();
        services.AddScoped<CashSessionReader>();
        services.AddScoped<SaleReader>();
        services.AddScoped<SaleReturnReader>();
        services.AddScoped<CreditPaymentReader>();

        return services;
    }
}
