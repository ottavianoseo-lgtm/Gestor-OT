using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionModeToLaborType : Migration
    {
        // Nota: el scaffold generó además un CreateTable de "LaborTypeAliases", porque el
        // ModelSnapshot no tenía esa entidad (la migración 20260908154500_AddLaborTypeAliasTable
        // se escribió a mano sin actualizarlo). La tabla ya existe y esa migración figura
        // aplicada, así que acá se quita el CreateTable: recrearla rompía el arranque con
        // 42P07 relation "LaborTypeAliases" already exists. El snapshot sí queda corregido,
        // que es lo que evita que la próxima migración vuelva a arrastrar el mismo fantasma.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExecutionMode",
                table: "LaborTypes",
                type: "integer",
                nullable: true);

            // Los tipos ya activados se clasifican solos: el subgrupo de conceptos del ERP,
            // que quedó guardado en Description, ya trae el modo escrito
            // ("LABORES POR HECTAREA (CONTRATISTA)" / "LABORES POR UTA (MAQ PROPIA)").
            // Sin esto arrancan todos sin clasificar y el filtro no filtra nada.
            // Valores del enum LaborExecutionMode: Own = 0, Contractor = 1.
            // Contratista va primero para que coincida con la precedencia de
            // LaborExecutionModeExtensions.InferFromErpSubGroup. Lo que el subgrupo no
            // aclara queda en NULL a propósito, para no adivinar.
            migrationBuilder.Sql("""
                UPDATE "LaborTypes"
                SET "ExecutionMode" = 1
                WHERE "ExecutionMode" IS NULL
                  AND "Description" IS NOT NULL
                  AND (upper("Description") LIKE '%CONTRATISTA%'
                       OR upper("Description") LIKE '%TERCERO%');
                """);

            migrationBuilder.Sql("""
                UPDATE "LaborTypes"
                SET "ExecutionMode" = 0
                WHERE "ExecutionMode" IS NULL
                  AND "Description" IS NOT NULL
                  AND (upper("Description") LIKE '%PROPIA%'
                       OR upper("Description") LIKE '%PROPIO%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionMode",
                table: "LaborTypes");
        }
    }
}
