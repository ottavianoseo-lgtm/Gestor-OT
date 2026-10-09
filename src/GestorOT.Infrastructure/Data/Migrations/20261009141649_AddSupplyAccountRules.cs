using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyAccountRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliesTo",
                table: "AccountConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CodListaDePrecios",
                table: "AccountConfigurations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplySubGroup",
                table: "AccountConfigurations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesTo",
                table: "AccountConfigurations");

            migrationBuilder.DropColumn(
                name: "CodListaDePrecios",
                table: "AccountConfigurations");

            migrationBuilder.DropColumn(
                name: "SupplySubGroup",
                table: "AccountConfigurations");
        }
    }
}
