using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionModeToErpConcept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExecutionMode",
                table: "ErpConcepts",
                type: "integer",
                nullable: true);

            // Los conceptos ya activados no arrancan de cero: si el LaborType que crearon ya
            // tiene el modo decidido (a mano o inferido al activar), se copia para acá. Es más
            // confiable que re-inferir del subgrupo, porque cubre también los casos donde el
            // subgrupo del ERP no lo decía (p.ej. "LABORES POR HECTAREA" sin "(CONTRATISTA)")
            // y el usuario lo eligió a mano en el modal.
            migrationBuilder.Sql("""
                UPDATE "ErpConcepts" AS c
                SET "ExecutionMode" = lt."ExecutionMode"
                FROM "LaborTypes" AS lt
                WHERE lt."ExternalErpId" = c."ExternalErpId"
                  AND lt."ExecutionMode" IS NOT NULL
                  AND c."ExecutionMode" IS NULL;
                """);

            // Lo que quedó sin cubrir (no activado, o activado antes de clasificarlo) se
            // infiere del subgrupo, igual que en 20260910124919_AddExecutionModeToLaborType.
            // Valores del enum LaborExecutionMode: Own = 0, Contractor = 1. Lo que el
            // subgrupo no aclara queda en NULL a propósito, para no adivinar.
            migrationBuilder.Sql("""
                UPDATE "ErpConcepts"
                SET "ExecutionMode" = 1
                WHERE "ExecutionMode" IS NULL
                  AND "SubGrupoConcepto" IS NOT NULL
                  AND (upper("SubGrupoConcepto") LIKE '%CONTRATISTA%'
                       OR upper("SubGrupoConcepto") LIKE '%TERCERO%');
                """);

            migrationBuilder.Sql("""
                UPDATE "ErpConcepts"
                SET "ExecutionMode" = 0
                WHERE "ExecutionMode" IS NULL
                  AND "SubGrupoConcepto" IS NOT NULL
                  AND (upper("SubGrupoConcepto") LIKE '%PROPIA%'
                       OR upper("SubGrupoConcepto") LIKE '%PROPIO%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionMode",
                table: "ErpConcepts");
        }
    }
}
