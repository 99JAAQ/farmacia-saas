using Farmacia.Application.Platform;
using Farmacia.Domain.Pharmacies;

namespace Farmacia.Application.Abstractions;

/// <summary>
/// The operations above the tenants, for the platform administrator. Implemented in Infrastructure because they need
/// Identity and must bypass Row-Level Security (a new pharmacy is, by definition, not the session's pharmacy).
/// </summary>
public interface IPlatformPharmacies
{
    /// <summary>Creates the pharmacy and its owner in one transaction: both exist, or neither does.</summary>
    /// <exception cref="Common.Exceptions.ConflictException">The NIT or the owner's email is already registered.</exception>
    /// <exception cref="Common.Exceptions.ValidationException">The email or the password is not valid.</exception>
    Task CreateAsync(Pharmacy pharmacy, NewOwner owner, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformPharmacyDto>> ListAsync(CancellationToken cancellationToken);
}

/// <param name="Password">Initial password, given to the owner by the administrator.</param>
public sealed record NewOwner(string FullName, string Email, string Password);
