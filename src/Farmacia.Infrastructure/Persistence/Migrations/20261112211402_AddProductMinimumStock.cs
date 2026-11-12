using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farmacia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMinimumStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumStock",
                table: "Products",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumStock",
                table: "Products");
        }
    }
}
