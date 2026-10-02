namespace GestorOT.Shared;

/// <summary>
/// Las fechas "de día" (ejecución y estimada de una labor, vencimiento de una OT) se guardan como
/// la medianoche de Argentina expresada en UTC (03:00Z), que es lo que manda la UI. Todo lo que
/// arme una de esas fechas fuera de la UI (importadores, MCP) tiene que pasar por acá; si no,
/// queda a las 00:00Z, se ve igual en pantalla pero los filtros por rango la corren un día.
/// </summary>
public static class LocalDay
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    /// <summary>Toma solo el día calendario de <paramref name="value"/> y lo lleva a la medianoche local en UTC.</summary>
    public static DateTime ToUtc(DateTime value) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified), Zone);

    public static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;
}
