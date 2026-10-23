namespace Farmacia.Infrastructure.Identity;

/// <summary>
/// One link of a session. Each refresh creates a new token in the same family and revokes the previous one,
/// so every token works once. A revoked token coming back means it was copied: the whole family is revoked.
/// Only the SHA-256 hash of the token is stored.
/// </summary>
public class RefreshToken
{
    private RefreshToken() { } // EF Core

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private init; }
    public Guid UserId { get; private init; }

    /// <summary>Shared by every token born from the same login.</summary>
    public Guid FamilyId { get; private init; }

    public string TokenHash { get; private init; } = null!;
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset ExpiresAt { get; private init; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void Revoke(DateTimeOffset now, string reason, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = now;
        RevokedReason = reason;
        ReplacedByTokenId = replacedByTokenId;
    }
}
