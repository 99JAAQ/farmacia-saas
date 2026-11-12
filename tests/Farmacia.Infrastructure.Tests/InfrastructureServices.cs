using System.Security.Cryptography;
using Farmacia.Application;
using Farmacia.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Infrastructure.Tests;

/// <summary>The real Application + Infrastructure services, wired to the test container.</summary>
internal static class InfrastructureServices
{
    /// <param name="pharmacyId">Defaults to the fixture's shared pharmacy.</param>
    /// <param name="currentUser">The user making the requests, for the use cases that need one.</param>
    /// <param name="noPharmacy">Requests without a pharmacy, like the platform administrator's.</param>
    public static ServiceProvider Build(
        SqlServerFixture fixture, TimeProvider? time = null, Guid? pharmacyId = null, ICurrentUser? currentUser = null,
        bool noPharmacy = false)
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
        services.AddSingleton<ICurrentPharmacy>(new FixedPharmacy(noPharmacy ? null : pharmacyId ?? fixture.PharmacyId));
        if (currentUser is not null)
            services.AddSingleton(currentUser);

        return services.BuildServiceProvider();
    }

    private sealed class FixedPharmacy(Guid? pharmacyId) : ICurrentPharmacy
    {
        public Guid PharmacyId => pharmacyId ?? throw new InvalidOperationException("This test's requests have no pharmacy.");
        public Guid? PharmacyIdOrNull => pharmacyId;
    }
}
