using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderFieldsToLaborImportPendingRow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "LaborImportPendingRows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderNumber",
                table: "LaborImportPendingRows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderResponsible",
                table: "LaborImportPendingRows",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "LaborImportPendingRows");

            migrationBuilder.DropColumn(
                name: "WorkOrderNumber",
                table: "LaborImportPendingRows");

            migrationBuilder.DropColumn(
                name: "WorkOrderResponsible",
                table: "LaborImportPendingRows");
        }
    }
}
