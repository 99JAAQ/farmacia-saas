using Farmacia.Application.Abstractions.Persistence;

namespace Farmacia.Infrastructure.Persistence;

/// <summary>
/// The last number a pharmacy used for one kind of document. A persistence detail, not a domain concept: only
/// <see cref="Repositories.DocumentNumberSequence"/> touches it, with one atomic SQL statement.
/// </summary>
public class DocumentCounter
{
    public Guid PharmacyId { get; set; }
    public NumberedDocument Document { get; set; }
    public long LastNumber { get; set; }
}
