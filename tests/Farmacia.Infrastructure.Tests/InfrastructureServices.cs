using System.Security.Cryptography;
using Farmacia.Application;
using Farmacia.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests;

/// <summary>The real Application + Infrastructure services, wired to the test container.</summary>
internal static class InfrastructureServices
{
    public static ServiceProvider Build(SqlServerFixture fixture, TimeProvider? time = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Farmacia"] = fixture.ConnectionString,
                ["Jwt:Issuer"] = "farmacia-api",
                ["Jwt:Audience"] = "farmacia-web",
                ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            })
            .Build();

        var services = new ServiceCollection().AddLogging();
        if (time is not null)
            services.AddSingleton(time); // registered first: AddInfrastructure only adds TimeProvider.System if missing

        services.AddApplication(mediatRLicenseKey: null);
        services.AddInfrastructure(configuration);
        services.AddSingleton<ICurrentPharmacy>(new FixedPharmacy(fixture.PharmacyId));

        return services.BuildServiceProvider();
    }

    private sealed class FixedPharmacy(Guid pharmacyId) : ICurrentPharmacy
    {
        public Guid PharmacyId { get; } = pharmacyId;
    }
}
