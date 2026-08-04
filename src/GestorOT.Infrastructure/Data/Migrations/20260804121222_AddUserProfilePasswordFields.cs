using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilePasswordFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "public",
                table: "UserProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordSalt",
                schema: "public",
                table: "UserProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "public",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PasswordSalt",
                schema: "public",
                table: "UserProfiles");
        }
    }
}
