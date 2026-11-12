using Microsoft.AspNetCore.Identity;

namespace Farmacia.Infrastructure.Identity;

/// <summary>
/// Identity user. Each user belongs to exactly one pharmacy, which goes into the JWT as the tenant; only the platform
/// administrator belongs to none (PharmacyId null), so its token carries no tenant at all.
/// Users log in with their email: Identity's UserName is unique across all tenants, so it is set to the email too.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid? PharmacyId { get; set; }
    public string FullName { get; set; } = null!;

    /// <summary>False for someone who no longer works at the pharmacy: cannot log in. Never deleted (sales point to them).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The password was set by someone else (the owner or the platform administrator): change it at login.</summary>
    public bool MustChangePassword { get; set; }
}
