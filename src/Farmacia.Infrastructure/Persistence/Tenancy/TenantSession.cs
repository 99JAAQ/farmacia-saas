namespace Farmacia.Infrastructure.Persistence.Tenancy;

/// <summary>
/// What a database connection carries in its SESSION_CONTEXT, which the Row-Level Security policy reads.
/// <list type="bullet">
/// <item><see cref="For"/>: sees and writes only that pharmacy's rows (every request with a token).</item>
/// <item><see cref="None"/>: sees no tenant rows at all (anonymous requests: the login only touches Identity).</item>
/// <item><see cref="Bypass"/>: sees every pharmacy (the development seeder, tests, creating a pharmacy).</item>
/// </list>
/// </summary>
public readonly record struct TenantSession
{
    private TenantSession(Guid? pharmacyId, bool bypassesIsolation) =>
        (PharmacyId, BypassesIsolation) = (pharmacyId, bypassesIsolation);

    public Guid? PharmacyId { get; }

    public bool BypassesIsolation { get; }

    public static TenantSession None => default;

    public static TenantSession Bypass => new(null, bypassesIsolation: true);

    public static TenantSession For(Guid pharmacyId) =>
        pharmacyId == Guid.Empty
            ? throw new ArgumentException("A tenant session needs a pharmacy.", nameof(pharmacyId))
            : new(pharmacyId, bypassesIsolation: false);
}
