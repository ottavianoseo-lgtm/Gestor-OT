using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairDuplicateLaborTypeErpIds : Migration
    {
        // El sync de LaborTypes matcheaba por "ExternalErpId == codigo OR Name == descripcion".
        // El ERP repite la misma tarea en dos subgrupos con codConcepto distinto, asi que al
        // procesar la segunda variante encontraba la fila de la primera por nombre y le pisaba
        // el ExternalErpId. Quedaron filas apuntando al codigo de la otra variante, algunas
        // duplicando un codigo ya ocupado y otras dejando su propio codigo sin fila.
        //
        // Eso rompia el desactivar: el endpoint borraba solo el primero de la lista, el gemelo
        // sobrevivia y el concepto seguia figurando como activado aunque la respuesta fuera Ok.
        //
        // Como el subgrupo quedo guardado en Description, la identidad real de cada fila se
        // puede reconstruir: es el concepto que tiene el mismo nombre y ese mismo subgrupo. Se
        // exige candidato unico para no adivinar.
        //
        // ExecutionMode no se toca: se derivo de Description, que es el subgrupo correcto.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Devolver a cada fila el codigo del concepto que realmente le corresponde.
            migrationBuilder.Sql("""
                WITH candidatos AS (
                    SELECT lt."Id", min(ec2."ExternalErpId") AS cod_real
                    FROM "LaborTypes" lt
                    LEFT JOIN "ErpConcepts" ec ON ec."ExternalErpId" = lt."ExternalErpId"
                    JOIN "ErpConcepts" ec2
                      ON ec2."Description" = lt."Name"
                     AND ec2."SubGrupoConcepto" = lt."Description"
                    WHERE lt."Description" IS NOT NULL
                      AND (ec."ExternalErpId" IS NULL
                           OR lt."Description" IS DISTINCT FROM ec."SubGrupoConcepto")
                    GROUP BY lt."Id"
                    HAVING count(*) = 1
                )
                UPDATE "LaborTypes" lt
                SET "ExternalErpId" = c.cod_real
                FROM candidatos c
                WHERE lt."Id" = c."Id"
                  AND lt."ExternalErpId" IS DISTINCT FROM c.cod_real;
                """);

            // 2. Deduplicar lo que quede: devolver el codigo real deja dos filas donde el
            //    concepto ya tenia la suya. Se conserva la fila mas referenciada (una labor
            //    apuntando a ella pesa mas que el orden del Id) y se borran las sobrantes,
            //    pero solo si nadie las referencia: mejor un duplicado que una FK rota, y el
            //    desactivar ya sabe sacar la lista completa.
            migrationBuilder.Sql("""
                WITH ordenadas AS (
                    SELECT lt."Id",
                           row_number() OVER (
                               PARTITION BY lt."ExternalErpId"
                               ORDER BY (
                                   (SELECT count(*) FROM "Labors" l WHERE l."LaborTypeId" = lt."Id")
                                 + (SELECT count(*) FROM "StrategyItems" si WHERE si."LaborTypeId" = lt."Id")
                                 + (SELECT count(*) FROM "LaborTypeAliases" a WHERE a."LaborTypeId" = lt."Id")
                               ) DESC, lt."Id"
                           ) AS puesto
                    FROM "LaborTypes" lt
                    WHERE lt."ExternalErpId" IS NOT NULL
                )
                DELETE FROM "LaborTypes" lt
                USING ordenadas o
                WHERE lt."Id" = o."Id"
                  AND o.puesto > 1
                  AND NOT EXISTS (SELECT 1 FROM "Labors" l WHERE l."LaborTypeId" = lt."Id")
                  AND NOT EXISTS (SELECT 1 FROM "StrategyItems" si WHERE si."LaborTypeId" = lt."Id")
                  AND NOT EXISTS (SELECT 1 FROM "LaborTypeAliases" a WHERE a."LaborTypeId" = lt."Id");
                """);
        }

        // Sin Down: devolver los codigos pisados seria volver a corromper los datos.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
