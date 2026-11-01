using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farmacia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementPerformedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PerformedBy",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PerformedBy",
                table: "StockMovements",
                column: "PerformedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_AspNetUsers_PerformedBy",
                table: "StockMovements",
                column: "PerformedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_AspNetUsers_PerformedBy",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PerformedBy",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PerformedBy",
                table: "StockMovements");
        }
    }
}
