using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodCentroToLotAndCampaignLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CodCentro",
                schema: "public",
                table: "Lots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CodCentro",
                schema: "public",
                table: "CampaignLots",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodCentro",
                schema: "public",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "CodCentro",
                schema: "public",
                table: "CampaignLots");
        }
    }
}
