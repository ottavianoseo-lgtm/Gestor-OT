using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSharedActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.AlterColumn<Guid>(
                name: "ErpActivityId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.InsertData(
                schema: "public",
                table: "ErpActivities",
                columns: new[] { "Id", "ExternalErpId", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e01"), null, "Trigo", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e02"), null, "Sorgo", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e03"), null, "Avena", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e04"), null, "Maní", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e05"), null, "Camelina", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e06"), null, "Alpiste", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e07"), null, "Girasol", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e08"), null, "Girasol alto oleico", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e09"), null, "Soja 1º", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e10"), null, "Soja 2º", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e11"), null, "Papa", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e12"), null, "Cebada cervezera", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e13"), null, "Cebada forrajera", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e14"), null, "Maíz", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e15"), null, "Maíz tardío", new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors",
                column: "ErpActivityId",
                principalSchema: "public",
                principalTable: "ErpActivities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e01"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e02"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e03"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e04"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e05"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e06"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e07"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e08"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e09"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e10"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e11"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e12"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e13"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e14"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e15"));

            migrationBuilder.AlterColumn<Guid>(
                name: "ErpActivityId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors",
                column: "ErpActivityId",
                principalSchema: "public",
                principalTable: "ErpActivities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
