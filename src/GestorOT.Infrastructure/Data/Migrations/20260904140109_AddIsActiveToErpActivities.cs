using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToErpActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "public",
                table: "ErpActivities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e01"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e02"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e03"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e04"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e05"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e06"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e07"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e08"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e09"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e10"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e11"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e12"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e13"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e14"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ErpActivities",
                keyColumn: "Id",
                keyValue: new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e15"),
                column: "IsActive",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "public",
                table: "ErpActivities");
        }
    }
}
