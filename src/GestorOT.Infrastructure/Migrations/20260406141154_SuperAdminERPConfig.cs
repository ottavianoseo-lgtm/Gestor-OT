using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuperAdminERPConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GestorMaxApiKeyEncrypted",
                schema: "public",
                table: "Tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GestorMaxDatabaseId",
                schema: "public",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GestorMaxApiKeyEncrypted",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GestorMaxDatabaseId",
                schema: "public",
                table: "Tenants");
        }
    }
}
