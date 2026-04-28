using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStrategyActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CropType",
                schema: "public",
                table: "CropStrategies");

            migrationBuilder.AddColumn<Guid>(
                name: "ErpActivityId",
                schema: "public",
                table: "StrategyItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ErpActivityId",
                schema: "public",
                table: "CropStrategies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CropStrategies_ErpActivityId",
                schema: "public",
                table: "CropStrategies",
                column: "ErpActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_CropStrategies_ErpActivities_ErpActivityId",
                schema: "public",
                table: "CropStrategies",
                column: "ErpActivityId",
                principalSchema: "public",
                principalTable: "ErpActivities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CropStrategies_ErpActivities_ErpActivityId",
                schema: "public",
                table: "CropStrategies");

            migrationBuilder.DropIndex(
                name: "IX_CropStrategies_ErpActivityId",
                schema: "public",
                table: "CropStrategies");

            migrationBuilder.DropColumn(
                name: "ErpActivityId",
                schema: "public",
                table: "StrategyItems");

            migrationBuilder.DropColumn(
                name: "ErpActivityId",
                schema: "public",
                table: "CropStrategies");

            migrationBuilder.AddColumn<string>(
                name: "CropType",
                schema: "public",
                table: "CropStrategies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
