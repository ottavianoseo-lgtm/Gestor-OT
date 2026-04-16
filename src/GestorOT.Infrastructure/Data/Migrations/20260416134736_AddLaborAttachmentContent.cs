using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLaborAttachmentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileUrl",
                schema: "public",
                table: "LaborAttachments");

            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                schema: "public",
                table: "LaborAttachments",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                schema: "public",
                table: "LaborAttachments");

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                schema: "public",
                table: "LaborAttachments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
