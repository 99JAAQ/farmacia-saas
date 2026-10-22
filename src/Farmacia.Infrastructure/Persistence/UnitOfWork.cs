using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Farmacia.Infrastructure.Persistence;

/// <summary>
/// Commits through the DbContext and translates database errors into use-case errors,
/// so the API answers 409 instead of 500 and no other layer needs to know about SQL Server.
/// </summary>
internal sealed class UnitOfWork(FarmaciaDbContext db) : IUnitOfWork
{
    // SQL Server: unique index violation (2601) and unique constraint violation (2627).
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("Los datos cambiaron mientras se procesaba la operación. Intente de nuevo.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number))
        {
            // The use case checks first, but two simultaneous requests can both pass that check:
            // the unique index is the final guard.
            throw new ConflictException("Ya existe un registro con esos datos (por ejemplo, un código de barras repetido).", ex);
        }
    }
}
