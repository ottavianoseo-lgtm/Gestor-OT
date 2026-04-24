using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLaborPriorityAndWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='Labors' AND column_name='Priority') THEN ALTER TABLE public.\"Labors\" ADD \"Priority\" integer NOT NULL DEFAULT 0; END IF; END $$;");

            migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='Labors' AND column_name='SupplyWithdrawalNotes') THEN ALTER TABLE public.\"Labors\" ADD \"SupplyWithdrawalNotes\" text; END IF; END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "public",
                table: "Labors");

            migrationBuilder.DropColumn(
                name: "SupplyWithdrawalNotes",
                schema: "public",
                table: "Labors");
        }
    }
}
