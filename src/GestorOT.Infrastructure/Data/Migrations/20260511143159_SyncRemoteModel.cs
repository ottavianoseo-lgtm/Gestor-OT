using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncRemoteModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedTransitionsJson",
                schema: "public",
                table: "WorkOrderStatuses",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "public",
                table: "WorkOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "public",
                table: "StrategyItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FileAssets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaborFileAssets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborFileAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborFileAssets_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "public",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaborFileAssets_Labors_LaborId",
                        column: x => x.LaborId,
                        principalSchema: "public",
                        principalTable: "Labors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyItems_LaborTypeId",
                schema: "public",
                table: "StrategyItems",
                column: "LaborTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborFileAssets_FileAssetId",
                schema: "public",
                table: "LaborFileAssets",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborFileAssets_LaborId_FileAssetId",
                schema: "public",
                table: "LaborFileAssets",
                columns: new[] { "LaborId", "FileAssetId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StrategyItems_LaborTypes_LaborTypeId",
                schema: "public",
                table: "StrategyItems",
                column: "LaborTypeId",
                principalTable: "LaborTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StrategyItems_LaborTypes_LaborTypeId",
                schema: "public",
                table: "StrategyItems");

            migrationBuilder.DropTable(
                name: "LaborFileAssets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "FileAssets",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_StrategyItems_LaborTypeId",
                schema: "public",
                table: "StrategyItems");

            migrationBuilder.DropColumn(
                name: "AllowedTransitionsJson",
                schema: "public",
                table: "WorkOrderStatuses");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "public",
                table: "StrategyItems");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "public",
                table: "WorkOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
