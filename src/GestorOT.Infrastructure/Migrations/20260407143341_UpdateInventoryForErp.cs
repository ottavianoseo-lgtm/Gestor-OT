using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInventoryForErp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalErpId",
                schema: "public",
                table: "Inventories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                schema: "public",
                table: "Inventories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalErpId",
                schema: "public",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "public",
                table: "Inventories");
        }
    }
}
