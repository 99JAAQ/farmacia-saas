using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farmacia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowUsersWithoutPharmacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PharmacyId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Edited by hand: EF would fill the platform administrators' PharmacyId with Guid.Empty, which breaks the
            // foreign key. Going back needs a decision about those users, so it stops and says so.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [PharmacyId] IS NULL)
                    THROW 50000, 'There are users without a pharmacy (platform administrators). Delete them or assign a pharmacy before reverting.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "PharmacyId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
