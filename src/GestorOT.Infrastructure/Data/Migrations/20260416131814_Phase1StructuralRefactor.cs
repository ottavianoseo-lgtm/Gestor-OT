using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1StructuralRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "HectareasTotales",
                schema: "public",
                table: "Fields");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkOrderStatusId",
                schema: "public",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedDose",
                schema: "public",
                table: "LaborSupplies",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedTotal",
                schema: "public",
                table: "LaborSupplies",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedHectares",
                schema: "public",
                table: "LaborSupplies",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RealHectares",
                schema: "public",
                table: "LaborSupplies",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                schema: "public",
                table: "Labors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Planned");

            migrationBuilder.AddColumn<Geometry>(
                name: "Geometry",
                schema: "public",
                table: "CampaignLots",
                type: "geometry(Geometry, 4326)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LaborAttachments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborAttachments_Labors_LaborId",
                        column: x => x.LaborId,
                        principalSchema: "public",
                        principalTable: "Labors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderStatuses",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsEditable = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderSupplyApprovals",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCalculated = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ApprovedWithdrawal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    WithdrawalCenter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RealTotalUsed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSupplyApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderSupplyApprovals_Inventories_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "public",
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSupplyApprovals_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "public",
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderStatusId",
                schema: "public",
                table: "WorkOrders",
                column: "WorkOrderStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborAttachments_LaborId",
                schema: "public",
                table: "LaborAttachments",
                column: "LaborId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSupplyApprovals_SupplyId",
                schema: "public",
                table: "WorkOrderSupplyApprovals",
                column: "SupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSupplyApprovals_WorkOrderId",
                schema: "public",
                table: "WorkOrderSupplyApprovals",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors",
                column: "ErpActivityId",
                principalSchema: "public",
                principalTable: "ErpActivities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_WorkOrderStatuses_WorkOrderStatusId",
                schema: "public",
                table: "WorkOrders",
                column: "WorkOrderStatusId",
                principalSchema: "public",
                principalTable: "WorkOrderStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_WorkOrderStatuses_WorkOrderStatusId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "LaborAttachments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "WorkOrderStatuses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "WorkOrderSupplyApprovals",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_WorkOrderStatusId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "WorkOrderStatusId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CalculatedDose",
                schema: "public",
                table: "LaborSupplies");

            migrationBuilder.DropColumn(
                name: "CalculatedTotal",
                schema: "public",
                table: "LaborSupplies");

            migrationBuilder.DropColumn(
                name: "PlannedHectares",
                schema: "public",
                table: "LaborSupplies");

            migrationBuilder.DropColumn(
                name: "RealHectares",
                schema: "public",
                table: "LaborSupplies");

            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "Geometry",
                schema: "public",
                table: "CampaignLots");

            migrationBuilder.AddColumn<double>(
                name: "HectareasTotales",
                schema: "public",
                table: "Fields",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors",
                column: "ErpActivityId",
                principalSchema: "public",
                principalTable: "ErpActivities",
                principalColumn: "Id");
        }
    }
}
