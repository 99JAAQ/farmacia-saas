using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Farmacia.Infrastructure.Persistence.Tenancy;

/// <summary>
/// Right after EF opens a connection, writes the tenant into its SESSION_CONTEXT so that the Row-Level Security
/// policy (migration AddRowLevelSecurity) filters every query by it, even one that forgets the WHERE.
/// </summary>
/// <remarks>
/// <para>
/// Both keys are always written, and read-only (@read_only = 1): nothing can change them on that connection, not even
/// injected SQL. Writing the bypass key as 0 matters as much as the pharmacy: if it were left unset, injected SQL
/// could set it to 1 and see every pharmacy.
/// </para>
/// <para>
/// Pooling is safe: when a pooled connection is handed out again, SQL Server resets it (sp_reset_connection), which
/// clears the session context. Every opening therefore starts empty and gets the current scope's tenant.
/// </para>
/// </remarks>
public sealed class TenantSessionInterceptor(Func<TenantSession> currentSession) : DbConnectionInterceptor
{
    public const string PharmacyKey = "PharmacyId";
    public const string BypassKey = "TenantBypass";

    private const string Sql = $"""
        EXEC sys.sp_set_session_context @key = N'{PharmacyKey}', @value = @pharmacyId, @read_only = 1;
        EXEC sys.sp_set_session_context @key = N'{BypassKey}', @value = @bypass, @read_only = 1;
        """;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection, currentSession());
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection, currentSession());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand CreateCommand(DbConnection connection, TenantSession session)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        // No pharmacy (anonymous request, or bypass) is NULL: the predicate compares nothing equal to it.
        AddParameter(command, "@pharmacyId", session.PharmacyId is { } id ? id : DBNull.Value, DbType.Guid);
        AddParameter(command, "@bypass", session.BypassesIsolation, DbType.Boolean);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
