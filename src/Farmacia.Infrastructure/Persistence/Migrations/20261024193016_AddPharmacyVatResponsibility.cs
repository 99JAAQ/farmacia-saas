using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farmacia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyVatResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue only fills the existing pharmacies, matching the domain default (VAT-responsible).
            // Edited by hand: EF generated false. The model has no default on purpose: with HasDefaultValue(true)
            // EF could never save false, because for a bool it cannot tell "false" from "not set".
            migrationBuilder.AddColumn<bool>(
                name: "IsVatResponsible",
                table: "Pharmacies",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVatResponsible",
                table: "Pharmacies");
        }
    }
}
