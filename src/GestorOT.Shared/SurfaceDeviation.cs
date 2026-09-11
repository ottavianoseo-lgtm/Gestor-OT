namespace GestorOT.Shared;

/// <summary>Qué tan lejos está el polígono dibujado de la superficie declarada.</summary>
public enum SurfaceDeviationLevel
{
    /// <summary>No hay superficie catastral cargada: no hay contra qué comparar.</summary>
    Unknown,
    Ok,
    Warning,
    Critical
}

public readonly record struct SurfaceDeviationResult(
    bool CanCompare,
    double DifferenceHa,
    double DeviationPercent,
    SurfaceDeviationLevel Level);

/// <summary>
/// Compara la superficie que sale de la geometría (PostGIS) contra la catastral declarada.
///
/// El cálculo no se rehace en el navegador a propósito: el área geométrica ya viene de
/// ST_Area(geography) y un segundo cálculo en JS daría un número distinto al del panel, al de
/// los reportes y al de los pases.
/// </summary>
public static class SurfaceDeviation
{
    public const double WarningPercent = 3.0;
    public const double CriticalPercent = 10.0;

    public static SurfaceDeviationResult Compare(double gisAreaHa, decimal cadastralAreaHa)
    {
        var cadastral = (double)cadastralAreaHa;

        // Sin catastral cargada, reportar "100% de desvío" seria señalar al lote equivocado:
        // lo que falta es el dato declarado, no el dibujado.
        if (cadastral <= 0)
        {
            return new SurfaceDeviationResult(false, 0, 0, SurfaceDeviationLevel.Unknown);
        }

        var difference = gisAreaHa - cadastral;
        var percent = Math.Abs(difference) / cadastral * 100.0;

        var level = percent > CriticalPercent
            ? SurfaceDeviationLevel.Critical
            : percent >= WarningPercent
                ? SurfaceDeviationLevel.Warning
                : SurfaceDeviationLevel.Ok;

        return new SurfaceDeviationResult(true, difference, percent, level);
    }
}
