using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceStrategyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceStrategyId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Labors_SourceStrategyId",
                schema: "public",
                table: "Labors",
                column: "SourceStrategyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_CropStrategies_SourceStrategyId",
                schema: "public",
                table: "Labors",
                column: "SourceStrategyId",
                principalSchema: "public",
                principalTable: "CropStrategies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_CropStrategies_SourceStrategyId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropIndex(
                name: "IX_Labors_SourceStrategyId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "SourceStrategyId",
                schema: "public",
                table: "Labors");
        }
    }
}
