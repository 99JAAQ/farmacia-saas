using Farmacia.Infrastructure.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farmacia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Row-Level Security (ADR 0014): a second line of tenant isolation, inside SQL Server. Written by hand: the EF
    /// model does not change. The table list is a snapshot of today's tenant tables; later migrations add theirs with
    /// AddTenantIsolation. AspNetUsers stays out on purpose: the login looks a user up before knowing the pharmacy.
    /// </summary>
    public partial class AddRowLevelSecurity : Migration
    {
        private static readonly string[] TenantTables =
        [
            "Batches", "CashMovements", "CashSessions", "Categories", "CreditAccounts", "CreditEntries",
            "CreditPayments", "Customers", "DocumentCounters", "Payments", "Products", "PurchaseItems", "Purchases",
            "SaleItemBatches", "SaleItems", "SaleReturnItemBatches", "SaleReturnItems", "SaleReturns", "Sales",
            "SaleUnits", "StockMovements", "SupplierPayments", "Suppliers",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"CREATE SCHEMA [{TenantIsolationMigrations.Schema}];");

            // A row is visible when it belongs to the session's pharmacy, or when the session bypasses isolation
            // (seeder, tests, creating a pharmacy). An empty session sees nothing: it fails closed.
            migrationBuilder.Sql($"""
                CREATE FUNCTION {TenantIsolationMigrations.Predicate}(@PharmacyId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS RETURN
                    SELECT 1 AS AccessGranted
                    WHERE @PharmacyId = CAST(SESSION_CONTEXT(N'{TenantSessionInterceptor.PharmacyKey}') AS uniqueidentifier)
                       OR CAST(SESSION_CONTEXT(N'{TenantSessionInterceptor.BypassKey}') AS bit) = 1;
                """);

            // The pharmacy row itself: its Id is the tenant. A policy needs at least one predicate to be created.
            migrationBuilder.Sql($"""
                CREATE SECURITY POLICY {TenantIsolationMigrations.Policy}
                    ADD FILTER PREDICATE {TenantIsolationMigrations.Predicate}([Id]) ON [dbo].[Pharmacies],
                    ADD BLOCK PREDICATE {TenantIsolationMigrations.Predicate}([Id]) ON [dbo].[Pharmacies] AFTER INSERT,
                    ADD BLOCK PREDICATE {TenantIsolationMigrations.Predicate}([Id]) ON [dbo].[Pharmacies] AFTER UPDATE
                WITH (STATE = ON, SCHEMABINDING = ON);
                """);

            foreach (var table in TenantTables)
                migrationBuilder.AddTenantIsolation(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP SECURITY POLICY {TenantIsolationMigrations.Policy};");
            migrationBuilder.Sql($"DROP FUNCTION {TenantIsolationMigrations.Predicate};");
            migrationBuilder.Sql($"DROP SCHEMA [{TenantIsolationMigrations.Schema}];");
        }
    }
}
