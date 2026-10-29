using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farmacia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentCounters",
                columns: table => new
                {
                    PharmacyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Document = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCounters", x => new { x.PharmacyId, x.Document });
                    table.ForeignKey(
                        name: "FK_DocumentCounters_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Edited by hand: EF dropped SaleCounters first. The ticket numbers already used move to the new table
            // (Document 1 = Sale), so the next ticket does not start again at 1 and collide with the unique index.
            migrationBuilder.Sql(
                "INSERT INTO DocumentCounters (PharmacyId, Document, LastNumber) SELECT PharmacyId, 1, LastNumber FROM SaleCounters;");

            migrationBuilder.DropTable(
                name: "SaleCounters");

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturns_AspNetUsers_ReturnedBy",
                        column: x => x.ReturnedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ConversionFactor = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleReturnItems_SaleUnits_SaleUnitId",
                        column: x => x.SaleUnitId,
                        principalTable: "SaleUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnItemBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleReturnItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnItemBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnItemBatches_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnItemBatches_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnItemBatches_SaleReturnItems_SaleReturnItemId",
                        column: x => x.SaleReturnItemId,
                        principalTable: "SaleReturnItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItemBatches_BatchId",
                table: "SaleReturnItemBatches",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItemBatches_PharmacyId",
                table: "SaleReturnItemBatches",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItemBatches_SaleReturnItemId",
                table: "SaleReturnItemBatches",
                column: "SaleReturnItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_PharmacyId",
                table: "SaleReturnItems",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_ProductId",
                table: "SaleReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_SaleItemId",
                table: "SaleReturnItems",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_SaleReturnId",
                table: "SaleReturnItems",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnItems_SaleUnitId",
                table: "SaleReturnItems",
                column: "SaleUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_CashSessionId",
                table: "SaleReturns",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_PharmacyId_CashSessionId",
                table: "SaleReturns",
                columns: new[] { "PharmacyId", "CashSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_PharmacyId_Number",
                table: "SaleReturns",
                columns: new[] { "PharmacyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_PharmacyId_SaleId",
                table: "SaleReturns",
                columns: new[] { "PharmacyId", "SaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_ReturnedBy",
                table: "SaleReturns",
                column: "ReturnedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_SaleId",
                table: "SaleReturns",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleReturnItemBatches");

            migrationBuilder.DropTable(
                name: "SaleReturnItems");

            migrationBuilder.DropTable(
                name: "SaleReturns");

            migrationBuilder.CreateTable(
                name: "SaleCounters",
                columns: table => new
                {
                    PharmacyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleCounters", x => x.PharmacyId);
                    table.ForeignKey(
                        name: "FK_SaleCounters_Pharmacies_PharmacyId",
                        column: x => x.PharmacyId,
                        principalTable: "Pharmacies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // The inverse of Up: the ticket numbers go back; the return numbers are lost with their table.
            migrationBuilder.Sql(
                "INSERT INTO SaleCounters (PharmacyId, LastNumber) SELECT PharmacyId, LastNumber FROM DocumentCounters WHERE Document = 1;");

            migrationBuilder.DropTable(
                name: "DocumentCounters");
        }
    }
}
