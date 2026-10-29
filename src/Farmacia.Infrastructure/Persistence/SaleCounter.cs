namespace Farmacia.Infrastructure.Persistence;

/// <summary>
/// The last ticket number used by a pharmacy. A persistence detail, not a domain concept: only
/// <see cref="Repositories.TicketNumberSequence"/> touches it, with one atomic SQL statement.
/// </summary>
public class SaleCounter
{
    public Guid PharmacyId { get; set; }
    public long LastNumber { get; set; }
}
