namespace Farmacia.Domain.Common;

public abstract class Entity
{
    // Generated in the domain (not by the database) so the offline POS can create records without Id collisions.
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
