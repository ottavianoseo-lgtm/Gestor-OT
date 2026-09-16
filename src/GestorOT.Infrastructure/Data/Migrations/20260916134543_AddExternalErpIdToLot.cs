using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalErpIdToLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalErpId",
                schema: "public",
                table: "Lots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lots_TenantId_ExternalErpId",
                schema: "public",
                table: "Lots",
                columns: new[] { "TenantId", "ExternalErpId" },
                unique: true,
                filter: "\"ExternalErpId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lots_TenantId_ExternalErpId",
                schema: "public",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "ExternalErpId",
                schema: "public",
                table: "Lots");
        }
    }
}
