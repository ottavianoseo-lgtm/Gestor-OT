using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedLaborId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlannedLaborId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Labors_PlannedLaborId",
                schema: "public",
                table: "Labors",
                column: "PlannedLaborId");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_Labors_PlannedLaborId",
                schema: "public",
                table: "Labors",
                column: "PlannedLaborId",
                principalSchema: "public",
                principalTable: "Labors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_Labors_PlannedLaborId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropIndex(
                name: "IX_Labors_PlannedLaborId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "PlannedLaborId",
                schema: "public",
                table: "Labors");
        }
    }
}
