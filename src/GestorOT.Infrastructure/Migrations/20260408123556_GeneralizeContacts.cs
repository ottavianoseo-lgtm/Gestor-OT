using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_EmployeeId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                schema: "public",
                table: "WorkOrders",
                newName: "ContactId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_EmployeeId",
                schema: "public",
                table: "WorkOrders",
                newName: "IX_WorkOrders_ContactId");

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    LegalName = table.Column<string>(type: "text", nullable: true),
                    VatNumber = table.Column<string>(type: "text", nullable: true),
                    ExternalErpId = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Contacts_ContactId",
                schema: "public",
                table: "WorkOrders",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Contacts_ContactId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.RenameColumn(
                name: "ContactId",
                schema: "public",
                table: "WorkOrders",
                newName: "EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_ContactId",
                schema: "public",
                table: "WorkOrders",
                newName: "IX_WorkOrders_EmployeeId");

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    ExternalErpId = table.Column<string>(type: "text", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_EmployeeId",
                schema: "public",
                table: "WorkOrders",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");
        }
    }
}
