using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint6_Final_Updates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='WorkOrders' AND column_name='Name') THEN ALTER TABLE public.\"WorkOrders\" ADD \"Name\" character varying(100); END IF; END $$;");

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                schema: "public",
                table: "Labors",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                schema: "public",
                table: "WorkOrders");

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                schema: "public",
                table: "Labors",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }
    }
}
