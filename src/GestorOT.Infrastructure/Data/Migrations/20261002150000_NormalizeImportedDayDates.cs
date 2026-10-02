using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeImportedDayDates : Migration
    {
        // Las fechas de día se guardan como medianoche de Argentina en UTC (03:00Z), que es lo que
        // manda la UI. El importador de labores las guardaba a las 00:00Z: en pantalla se ven igual,
        // pero los filtros por rango (search_labors dateFrom/dateTo, que arman la medianoche local)
        // corrían esas labores un día.
        //
        // Ninguna pantalla genera 00:00:00Z exacto (en Argentina es las 21 del día anterior), así que
        // ese valor identifica las fechas mal guardadas sin adivinar. Se conserva el día calendario
        // UTC, que es el día que traía el Excel.
        private static readonly (string Table, string[] Columns)[] DayColumns =
        {
            ("Labors", new[] { "ExecutionDate", "EstimatedDate" }),
            ("WorkOrders", new[] { "DueDate", "PlannedDate", "ExpirationDate" }),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, columns) in DayColumns)
                foreach (var column in columns)
                    migrationBuilder.Sql($"""
                        UPDATE "{table}"
                        SET "{column}" = (("{column}" AT TIME ZONE 'UTC')::date::timestamp) AT TIME ZONE 'America/Argentina/Buenos_Aires'
                        WHERE ("{column}" AT TIME ZONE 'UTC')::time = '00:00:00';
                        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta: después del Up no se distingue una fecha corregida de una cargada por la UI.
        }
    }
}
