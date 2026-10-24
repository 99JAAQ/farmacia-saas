using Farmacia.Application.Abstractions;

namespace Farmacia.Infrastructure.Time;

internal sealed class ColombiaClock(TimeProvider time) : IClock
{
    // Colombia has had no daylight saving time since 1993: a fixed offset gives the same result as the
    // "America/Bogota" time zone without depending on time zone data being installed in the container.
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    public DateTimeOffset Now => time.GetUtcNow().ToOffset(ColombiaOffset);
}
