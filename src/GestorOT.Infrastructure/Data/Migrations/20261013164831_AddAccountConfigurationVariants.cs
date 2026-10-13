using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountConfigurationVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountConfigurationVariants",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ErpActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodPerfilDebe = table.Column<long>(type: "bigint", nullable: true),
                    CodPerfilHaber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountConfigurationVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountConfigurationVariants_AccountConfigurations_AccountC~",
                        column: x => x.AccountConfigurationId,
                        principalTable: "AccountConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountConfigurationVariants_ErpActivities_ErpActivityId",
                        column: x => x.ErpActivityId,
                        principalSchema: "public",
                        principalTable: "ErpActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountConfigurationVariants_AccountConfigurationId_ErpActi~",
                schema: "public",
                table: "AccountConfigurationVariants",
                columns: new[] { "AccountConfigurationId", "ErpActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountConfigurationVariants_ErpActivityId",
                schema: "public",
                table: "AccountConfigurationVariants",
                column: "ErpActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountConfigurationVariants",
                schema: "public");
        }
    }
}
