using Farmacia.Application.Cash;
using Farmacia.Application.Credit;
using Farmacia.Application.Purchases;
using Farmacia.Application.SaleReturns;
using Farmacia.Application.Sales;
using Microsoft.Extensions.DependencyInjection;

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
        });

        services.AddScoped<PurchaseReader>();
        services.AddScoped<CashSessionReader>();
        services.AddScoped<SaleReader>();
        services.AddScoped<SaleReturnReader>();
        services.AddScoped<CreditPaymentReader>();

        return services;
    }
}
