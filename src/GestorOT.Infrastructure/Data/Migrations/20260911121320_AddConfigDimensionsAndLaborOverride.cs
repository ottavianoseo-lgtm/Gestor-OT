using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigDimensionsAndLaborOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountConfigurationId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ErpActivityId",
                table: "AccountConfigurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionMode",
                table: "AccountConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Labors_AccountConfigurationId",
                schema: "public",
                table: "Labors",
                column: "AccountConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountConfigurations_ErpActivityId",
                table: "AccountConfigurations",
                column: "ErpActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountConfigurations_ErpActivities_ErpActivityId",
                table: "AccountConfigurations",
                column: "ErpActivityId",
                principalSchema: "public",
                principalTable: "ErpActivities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_AccountConfigurations_AccountConfigurationId",
                schema: "public",
                table: "Labors",
                column: "AccountConfigurationId",
                principalTable: "AccountConfigurations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountConfigurations_ErpActivities_ErpActivityId",
                table: "AccountConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_Labors_AccountConfigurations_AccountConfigurationId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropIndex(
                name: "IX_Labors_AccountConfigurationId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropIndex(
                name: "IX_AccountConfigurations_ErpActivityId",
                table: "AccountConfigurations");

            migrationBuilder.DropColumn(
                name: "AccountConfigurationId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "ErpActivityId",
                table: "AccountConfigurations");

            migrationBuilder.DropColumn(
                name: "ExecutionMode",
                table: "AccountConfigurations");
        }
    }
}
