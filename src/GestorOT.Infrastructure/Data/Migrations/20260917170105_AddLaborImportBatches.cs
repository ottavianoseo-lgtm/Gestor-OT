using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLaborImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaborImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FileHash = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ImportedCount = table.Column<int>(type: "integer", nullable: false),
                    PendingCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    SupplyMappingsJson = table.Column<string>(type: "text", nullable: false),
                    LaborTypeMappingsJson = table.Column<string>(type: "text", nullable: false),
                    SupplierMappingsJson = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborImportBatches_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "public",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaborImportPendingRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    LotName = table.Column<string>(type: "text", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignLotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Hectares = table.Column<decimal>(type: "numeric", nullable: false),
                    LaborTypeName = table.Column<string>(type: "text", nullable: false),
                    LaborTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Contractor = table.Column<string>(type: "text", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedContactName = table.Column<string>(type: "text", nullable: true),
                    IsExternalBilling = table.Column<bool>(type: "boolean", nullable: false),
                    SuppliesJson = table.Column<string>(type: "text", nullable: false),
                    ErrorsJson = table.Column<string>(type: "text", nullable: false),
                    WarningsJson = table.Column<string>(type: "text", nullable: false),
                    Resolution = table.Column<int>(type: "integer", nullable: false),
                    ResultLaborId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborImportPendingRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborImportPendingRows_LaborImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "LaborImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaborImportBatches_CampaignId",
                table: "LaborImportBatches",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborImportPendingRows_BatchId",
                table: "LaborImportPendingRows",
                column: "BatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaborImportPendingRows");

            migrationBuilder.DropTable(
                name: "LaborImportBatches");
        }
    }
}
