using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFinalStructuralChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreedRate",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "EstimatedCostUSD",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.AddColumn<bool>(
                name: "AcceptsMultipleDates",
                schema: "public",
                table: "WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptsMultiplePeople",
                schema: "public",
                table: "WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsMultipleDates",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AcceptsMultiplePeople",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedRate",
                schema: "public",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUSD",
                schema: "public",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
