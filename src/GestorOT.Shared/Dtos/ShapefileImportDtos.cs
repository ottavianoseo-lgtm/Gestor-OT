namespace GestorOT.Shared.Dtos;

/// <summary>Un polígono leído del shapefile, ya reproyectado a WGS84 (EPSG:4326).</summary>
public record ShapefileFeatureDto(
    string Name,
    string Wkt,
    double AreaHa,
    Dictionary<string, string> Attributes,
    /// <summary>Shapefile del que salió, cuando el zip trae varios. Aditivo: null en los viejos.</summary>
    string? SourceShapefile = null
)
{
    public ShapefileFeatureDto() : this(string.Empty, string.Empty, 0, new()) { }
}

/// <summary>
/// Un shapefile dentro del zip. Cada uno trae su propio CRS y sus propias columnas, así que el
/// detalle no se puede resumir a nivel raíz sin perder información.
/// </summary>
public record ShapefileGroupDto(
    string Name,
    int FeatureCount,
    string? SourceCrs,
    bool Reprojected,
    List<string> AttributeColumns,
    string? NameColumn
)
{
    public ShapefileGroupDto() : this(string.Empty, 0, null, false, new(), null) { }
}

public record ShapefileImportResultDto(
    List<ShapefileFeatureDto> Features,
    /// <summary>CRS del primer grupo. Se mantiene a nivel raíz por compatibilidad; el detalle por shapefile está en <see cref="Groups"/>.</summary>
    string? SourceCrs,
    /// <summary>Si se aplicó reproyección. False significa que se asumió que ya venía en 4326.</summary>
    bool Reprojected,
    /// <summary>Columnas del .dbf del primer grupo, para poder elegir cuál es el nombre del lote.</summary>
    List<string> AttributeColumns,
    /// <summary>Columna que se usó como nombre en el primer grupo.</summary>
    string? NameColumn,
    List<string> Warnings,
    /// <summary>Un elemento por shapefile encontrado en el zip. Aditivo.</summary>
    List<ShapefileGroupDto>? Groups = null
)
{
    public ShapefileImportResultDto() : this(new(), null, false, new(), null, new()) { }
}
