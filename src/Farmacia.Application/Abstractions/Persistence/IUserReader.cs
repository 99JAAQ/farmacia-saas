namespace Farmacia.Application.Abstractions.Persistence;

/// <summary>Read-only access to the pharmacy's users (they live in Identity, in Infrastructure).</summary>
public interface IUserReader
{
    /// <summary>Full names by user id. Ids of other pharmacies are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        Guid pharmacyId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
