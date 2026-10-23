using Microsoft.AspNetCore.Identity;

namespace Farmacia.Infrastructure.Identity;

/// <summary>
/// Identity user. Each user belongs to exactly one pharmacy, which goes into the JWT as the tenant.
/// Users log in with their email: Identity's UserName is unique across all tenants, so it is set to the email too.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid PharmacyId { get; set; }
    public string FullName { get; set; } = null!;
}
