using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FieldCentricRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Lots_LotId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.AlterColumn<Guid>(
                name: "LotId",
                schema: "public",
                table: "WorkOrders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FieldId",
                schema: "public",
                table: "WorkOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalBilling",
                schema: "public",
                table: "WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                schema: "public",
                table: "Labors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalBilling",
                schema: "public",
                table: "Labors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ErpPersonId",
                table: "Contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ErpPeople",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalErpId = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: true),
                    VatNumber = table.Column<string>(type: "text", nullable: true),
                    PersonType = table.Column<string>(type: "text", nullable: true),
                    DocumentType = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    ResponsibleTax = table.Column<string>(type: "text", nullable: true),
                    Group = table.Column<string>(type: "text", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActivated = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpPeople", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_FieldId",
                schema: "public",
                table: "WorkOrders",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_Labors_ContactId",
                schema: "public",
                table: "Labors",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ErpPersonId",
                table: "Contacts",
                column: "ErpPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_ErpPeople_ErpPersonId",
                table: "Contacts",
                column: "ErpPersonId",
                principalTable: "ErpPeople",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Labors_Contacts_ContactId",
                schema: "public",
                table: "Labors",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Fields_FieldId",
                schema: "public",
                table: "WorkOrders",
                column: "FieldId",
                principalSchema: "public",
                principalTable: "Fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Lots_LotId",
                schema: "public",
                table: "WorkOrders",
                column: "LotId",
                principalSchema: "public",
                principalTable: "Lots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_ErpPeople_ErpPersonId",
                table: "Contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Labors_Contacts_ContactId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Fields_FieldId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Lots_LotId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "ErpPeople");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_FieldId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Labors_ContactId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_ErpPersonId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "FieldId",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsExternalBilling",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ContactId",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "IsExternalBilling",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "ErpPersonId",
                table: "Contacts");

            migrationBuilder.AlterColumn<Guid>(
                name: "LotId",
                schema: "public",
                table: "WorkOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Lots_LotId",
                schema: "public",
                table: "WorkOrders",
                column: "LotId",
                principalSchema: "public",
                principalTable: "Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
