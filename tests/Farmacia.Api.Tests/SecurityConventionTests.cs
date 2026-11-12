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
    public async Task FallbackPolicy_RequiresAnAuthenticatedUserOfAPharmacy()
    {
        var policies = api.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await policies.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        // ...of a pharmacy: the platform administrator (no pharmacy_id) stays out of every business endpoint.
        Assert.Contains(fallback.Requirements, r =>
            r is Microsoft.AspNetCore.Authorization.Infrastructure.ClaimsAuthorizationRequirement claim
            && claim.ClaimType == Farmacia.Application.Authorization.ClaimNames.PharmacyId);
    }

    [Fact]
    public async Task EveryPolicy_RefusesATemporaryPassword()
    {
        // A named policy without it would let the owner use a cashier's account with the password they gave them.
        var policies = api.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var names = typeof(Farmacia.Api.Authentication.Policies)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        Assert.NotEmpty(names);

        foreach (var name in names)
        {
            var policy = await policies.GetPolicyAsync(name);
            Assert.NotNull(policy);
            Assert.Contains(policy.Requirements, r => r is Farmacia.Api.Authentication.PasswordChangedRequirement);
        }

        var fallback = await policies.GetFallbackPolicyAsync();
        Assert.Contains(fallback!.Requirements, r => r is Farmacia.Api.Authentication.PasswordChangedRequirement);
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
