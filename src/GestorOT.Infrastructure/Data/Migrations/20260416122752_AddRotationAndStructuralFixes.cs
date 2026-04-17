using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationAndStructuralFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_CampaignLots_CampaignLotId",
                schema: "public",
                table: "Labors");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignLotId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ErpActivityId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrupoConcepto",
                schema: "public",
                table: "Inventories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubGrupoConcepto",
                schema: "public",
                table: "Inventories",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "public",
                table: "Campaigns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Planning");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                schema: "public",
                table: "Campaigns",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ErpActivities",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rotations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ErpActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rotations_CampaignLots_CampaignLotId",
                        column: x => x.CampaignLotId,
                        principalSchema: "public",
                        principalTable: "CampaignLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rotations_ErpActivities_ErpActivityId",
                        column: x => x.ErpActivityId,
                        principalSchema: "public",
                        principalTable: "ErpActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Labors_ErpActivityId",
                schema: "public",
                table: "Labors",
                column: "ErpActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Rotations_CampaignLotId",
                schema: "public",
                table: "Rotations",
                column: "CampaignLotId");

            migrationBuilder.CreateIndex(
                name: "IX_Rotations_ErpActivityId",
                schema: "public",
                table: "Rotations",
                column: "ErpActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_CampaignLots_CampaignLotId",
                schema: "public",
                table: "Labors",
                column: "CampaignLotId",
                principalSchema: "public",
                principalTable: "CampaignLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labors_CampaignLots_CampaignLotId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropForeignKey(
                name: "FK_Labors_ErpActivities_ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropTable(
                name: "Rotations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ErpActivities",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Labors_ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "ErpActivityId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "GrupoConcepto",
                schema: "public",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "SubGrupoConcepto",
                schema: "public",
                table: "Inventories");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignLotId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "public",
                table: "Campaigns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Planning",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                schema: "public",
                table: "Campaigns",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_CampaignLots_CampaignLotId",
                schema: "public",
                table: "Labors",
                column: "CampaignLotId",
                principalSchema: "public",
                principalTable: "CampaignLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
