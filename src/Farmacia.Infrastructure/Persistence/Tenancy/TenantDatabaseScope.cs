using Farmacia.Application.Abstractions;

namespace Farmacia.Infrastructure.Persistence.Tenancy;

/// <summary>
/// Scoped (one per request): which tenant this scope's database connections carry. By default, the pharmacy of the
/// token; with no token (login, refresh), none, so the business tables look empty.
/// </summary>
public sealed class TenantDatabaseScope(ICurrentPharmacy currentPharmacy)
{
    private bool _bypass;

    /// <summary>
    /// Every pharmacy's rows, for the few trusted paths that work above the tenants: the development seeder, test
    /// setup and (phase 3) creating a pharmacy. Call it before the first query: a connection that is already open
    /// keeps the context it got. Controllers must never use it (SecurityConventionTests).
    /// </summary>
    public void BypassIsolation() => _bypass = true;

    public TenantSession Session =>
        _bypass ? TenantSession.Bypass
        : currentPharmacy.PharmacyIdOrNull is { } pharmacyId ? TenantSession.For(pharmacyId)
        : TenantSession.None;
}
