using Farmacia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Identity;

internal static class Sessions
{
    /// <summary>
    /// Ends every session of a user: no refresh token of theirs works any more. Their current access token still
    /// lasts until it expires (15 minutes at most).
    /// </summary>
    public static Task RevokeAllAsync(
        this FarmaciaDbContext db, Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason), cancellationToken);
}
