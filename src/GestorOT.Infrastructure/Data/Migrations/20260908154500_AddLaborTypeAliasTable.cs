using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLaborTypeAliasTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaborTypeAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RawName = table.Column<string>(type: "text", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false),
                    LaborTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborTypeAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborTypeAliases_LaborTypes_LaborTypeId",
                        column: x => x.LaborTypeId,
                        principalSchema: "public",
                        principalTable: "LaborTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaborTypeAliases_LaborTypeId",
                table: "LaborTypeAliases",
                column: "LaborTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborTypeAliases_TenantId_NormalizedName",
                table: "LaborTypeAliases",
                columns: new[] { "TenantId", "NormalizedName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaborTypeAliases");
        }
    }
}
