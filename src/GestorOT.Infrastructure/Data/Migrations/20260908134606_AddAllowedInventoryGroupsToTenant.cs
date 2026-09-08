using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowedInventoryGroupsToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedInventoryGroupsJson",
                schema: "public",
                table: "Tenants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedInventoryGroupsJson",
                schema: "public",
                table: "Tenants");
        }
    }
}
