using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierContactIdToLaborSupply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierContactId",
                schema: "public",
                table: "LaborSupplies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaborSupplies_SupplierContactId",
                schema: "public",
                table: "LaborSupplies",
                column: "SupplierContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_LaborSupplies_Contacts_SupplierContactId",
                schema: "public",
                table: "LaborSupplies",
                column: "SupplierContactId",
                principalTable: "Contacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LaborSupplies_Contacts_SupplierContactId",
                schema: "public",
                table: "LaborSupplies");

            migrationBuilder.DropIndex(
                name: "IX_LaborSupplies_SupplierContactId",
                schema: "public",
                table: "LaborSupplies");

            migrationBuilder.DropColumn(
                name: "SupplierContactId",
                schema: "public",
                table: "LaborSupplies");
        }
    }
}
