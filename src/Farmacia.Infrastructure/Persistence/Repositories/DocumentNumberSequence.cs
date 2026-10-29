using Farmacia.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// One atomic statement: it increments the pharmacy's counter for the document (or creates it with 1) and returns
/// the new value. Reading the number and saving it back in two steps would let two sales read the same one.
/// HOLDLOCK keeps the row (or, for the first document, the empty key range) locked until the transaction commits,
/// so a simultaneous use case waits a few milliseconds instead of failing.
/// </summary>
internal sealed class DocumentNumberSequence(FarmaciaDbContext db) : IDocumentNumberSequence
{
    public async Task<long> NextAsync(Guid pharmacyId, NumberedDocument document, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Reserve document numbers inside a transaction: outside one, a failed use case would skip a number.");

        var kind = (int)document;

        // ToListAsync, not SingleAsync: EF would wrap a composed query in a SELECT, and MERGE cannot be a subquery.
        var numbers = await db.Database
            .SqlQuery<long>($"""
                MERGE DocumentCounters WITH (HOLDLOCK) AS counter
                USING (SELECT {pharmacyId} AS PharmacyId, {kind} AS Document) AS source
                    ON counter.PharmacyId = source.PharmacyId AND counter.Document = source.Document
                WHEN MATCHED THEN UPDATE SET LastNumber = counter.LastNumber + 1
                WHEN NOT MATCHED THEN INSERT (PharmacyId, Document, LastNumber) VALUES (source.PharmacyId, source.Document, 1)
                OUTPUT inserted.LastNumber AS [Value];
                """)
            .ToListAsync(cancellationToken);

        return numbers.Single();
    }
}
