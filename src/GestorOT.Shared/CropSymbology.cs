namespace GestorOT.Shared;

/// <summary>Una rotación de un lote en una campaña, reducida a lo que el mapa necesita.</summary>
public readonly record struct CampaignCrop(
    Guid CropId,
    string CropName,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>El cultivo de un lote, ya resuelto y con su color.</summary>
public readonly record struct LotCropInfo(Guid CropId, string CropName, string Color);

/// <summary>
/// Cuál de las rotaciones de un lote es "el cultivo" que se muestra en el mapa.
///
/// Hace falta una regla explícita porque un lote puede tener más de una rotación en la misma
/// campaña (trigo y después soja). El reporte de trazabilidad resuelve esto con
/// <c>Rotations.FirstOrDefault()</c> sin ordenar, que devuelve cualquiera de las dos según lo
/// que traiga la base: acá no sirve, porque si el cultivo cambia solo entre recargas el color
/// determinístico de la leyenda pierde sentido.
/// </summary>
public static class CropSelection
{
    public static CampaignCrop? ForDate(IEnumerable<CampaignCrop> rotations, DateOnly today)
    {
        var ordered = rotations
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.EndDate)
            .ToList();

        if (ordered.Count == 0) return null;

        // Lo que hay sembrado hoy, que es lo que se quiere leer del mapa.
        foreach (var rotation in ordered)
        {
            if (rotation.StartDate <= today && rotation.EndDate >= today) return rotation;
        }

        // Campaña ya terminada: se muestra el último cultivo que llegó a sembrarse, en vez de
        // dejar el lote en gris como si nunca hubiera tenido nada.
        var started = ordered.Where(r => r.StartDate <= today).ToList();
        if (started.Count > 0) return started[^1];

        // Campaña que todavía no arrancó: lo primero planificado.
        return ordered[0];
    }
}

/// <summary>
/// Color por cultivo.
///
/// El color sale de la posición del cultivo en el catálogo de actividades del ERP, ordenado
/// por nombre. Así dos cultivos distintos nunca comparten color mientras entren en la paleta,
/// que es lo que hace legible el mapa: repartir por hash hacía que Trigo y Maíz —dos de los
/// tres cultivos más comunes— cayeran en el mismo tono.
///
/// A cambio, dar de alta una actividad que ordene antes que otras corre los colores de las
/// siguientes. Es un evento de catálogo, poco frecuente, y se prefirió eso a las colisiones.
///
/// El color se calcula una sola vez del lado del servidor y viaja hasta el polígono y hasta la
/// leyenda. Calcularlo por separado en JS y en C# serían dos implementaciones que pueden
/// discrepar sin que nadie se entere.
/// </summary>
public static class CropPalette
{
    /// <summary>Reservado para el lote sin cultivo asignado en la campaña.</summary>
    public const string NoCrop = "#7F8C8D";

    // Ninguno gris ni cercano al gris: el gris significa "sin cultivo" y un cultivo real que
    // cayera ahí haría mentir a la leyenda.
    private static readonly string[] Colors =
    [
        "#27AE60", "#E67E22", "#2980B9", "#8E44AD", "#F1C40F", "#16A085",
        "#C0392B", "#E84393", "#795548", "#00BCD4", "#9BCB3B", "#6C5CE7"
    ];

    public static int Count => Colors.Length;

    /// <param name="catalogIndex">
    /// Posición del cultivo en el catálogo ordenado. En null —un cultivo que no está en el
    /// catálogo del tenant, por ejemplo una actividad global— se cae a un hash del nombre, que
    /// puede colisionar pero al menos es estable.
    /// </param>
    public static string ColorFor(string? cropName, int? catalogIndex = null)
    {
        if (string.IsNullOrWhiteSpace(cropName)) return NoCrop;

        if (catalogIndex is int index && index >= 0)
        {
            return Colors[index % Colors.Length];
        }

        return Colors[StableHash(cropName) % (uint)Colors.Length];
    }

    /// <summary>
    /// Hash propio y no string.GetHashCode(): desde .NET Core ese está aleatorizado por
    /// proceso, así que el color cambiaría en cada reinicio del servidor.
    /// </summary>
    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (var c in value.Trim().ToUpperInvariant())
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }
}
