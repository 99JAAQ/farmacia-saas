using Microsoft.EntityFrameworkCore.Migrations;

namespace Farmacia.Infrastructure.Persistence.Tenancy;

/// <summary>
/// SQL for the Row-Level Security policy. A migration that creates a table with PharmacyId must call
/// <see cref="AddTenantIsolation"/> for it (and <see cref="RemoveTenantIsolation"/> before dropping it):
/// RowLevelSecurityTests fails if a tenant table is left out.
/// </summary>
public static class TenantIsolationMigrations
{
    public const string Schema = "tenancy";
    public const string Policy = "tenancy.PharmacyIsolation";
    public const string Predicate = "tenancy.fn_TenantAccess";

    /// <summary>
    /// FILTER: SELECT, UPDATE and DELETE only see the session's pharmacy.
    /// BLOCK AFTER INSERT / AFTER UPDATE: a row cannot be written into, or moved to, another pharmacy.
    /// </summary>
    public static void AddTenantIsolation(this MigrationBuilder migrationBuilder, string table, string column = "PharmacyId") =>
        migrationBuilder.Sql($"""
            ALTER SECURITY POLICY {Policy}
                ADD FILTER PREDICATE {Predicate}([{column}]) ON [dbo].[{table}],
                ADD BLOCK PREDICATE {Predicate}([{column}]) ON [dbo].[{table}] AFTER INSERT,
                ADD BLOCK PREDICATE {Predicate}([{column}]) ON [dbo].[{table}] AFTER UPDATE;
            """);

    public static void RemoveTenantIsolation(this MigrationBuilder migrationBuilder, string table) =>
        migrationBuilder.Sql($"""
            ALTER SECURITY POLICY {Policy}
                DROP FILTER PREDICATE ON [dbo].[{table}],
                DROP BLOCK PREDICATE ON [dbo].[{table}] AFTER INSERT,
                DROP BLOCK PREDICATE ON [dbo].[{table}] AFTER UPDATE;
            """);
}
