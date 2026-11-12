using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Farmacia.Api.Tests;

/// <summary>
/// Guards the security setup itself, not a single endpoint: these fail if someone weakens it by accident.
/// </summary>
[Collection(ApiCollection.Name)]
public class SecurityConventionTests(ApiFactory api)
{
    [Fact]
    public async Task FallbackPolicy_RequiresAnAuthenticatedUser()
    {
        var policies = api.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await policies.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void OnlyTheExpectedEndpoints_AllowAnonymousAccess()
    {
        // Adding a public endpoint must be a conscious decision: update this list on purpose.
        string[] expected = ["api/auth/login", "api/auth/logout", "api/auth/refresh", "health"];

        var anonymous = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText!.Trim('/'))
            .Order()
            .ToArray();

        Assert.Equal(expected, anonymous);
    }

    [Fact]
    public void NoControllerOrUseCase_CanBypassRowLevelSecurity()
    {
        // The bypass sees every pharmacy: only the seeder, test setup and (later) creating a pharmacy may use it.
        var types = typeof(Program).Assembly.GetTypes()
            .Concat(typeof(Farmacia.Application.DependencyInjection).Assembly.GetTypes());

        var offenders = types
            .Where(t => t.GetConstructors().Any(c => c.GetParameters()
                .Any(p => p.ParameterType == typeof(Farmacia.Infrastructure.Persistence.Tenancy.TenantDatabaseScope))))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }
}
