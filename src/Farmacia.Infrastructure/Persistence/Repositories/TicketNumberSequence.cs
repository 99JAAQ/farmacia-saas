using Farmacia.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence.Repositories;

/// <summary>
/// One atomic statement: it increments the pharmacy's counter (or creates it with 1) and returns the new value.
/// Reading the number and saving it back in two steps would let two sales read the same one.
/// HOLDLOCK keeps the row (or, for the first sale, the empty key range) locked until the transaction commits,
/// so a simultaneous sale waits a few milliseconds instead of failing.
/// </summary>
internal sealed class TicketNumberSequence(FarmaciaDbContext db) : ITicketNumberSequence
{
    public async Task<long> NextAsync(Guid pharmacyId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Reserve ticket numbers inside a transaction: outside one, a failed sale would skip a number.");

        // ToListAsync, not SingleAsync: EF would wrap a composed query in a SELECT, and MERGE cannot be a subquery.
        var numbers = await db.Database
            .SqlQuery<long>($"""
                MERGE SaleCounters WITH (HOLDLOCK) AS counter
                USING (SELECT {pharmacyId} AS PharmacyId) AS source ON counter.PharmacyId = source.PharmacyId
                WHEN MATCHED THEN UPDATE SET LastNumber = counter.LastNumber + 1
                WHEN NOT MATCHED THEN INSERT (PharmacyId, LastNumber) VALUES (source.PharmacyId, 1)
                OUTPUT inserted.LastNumber AS [Value];
                """)
            .ToListAsync(cancellationToken);

        return numbers.Single();
    }
}
