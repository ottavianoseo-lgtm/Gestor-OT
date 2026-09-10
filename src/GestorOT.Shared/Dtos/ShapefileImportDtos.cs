namespace GestorOT.Shared.Dtos;

/// <summary>Un polígono leído del shapefile, ya reproyectado a WGS84 (EPSG:4326).</summary>
public record ShapefileFeatureDto(
    string Name,
    string Wkt,
    double AreaHa,
    Dictionary<string, string> Attributes
)
{
    public ShapefileFeatureDto() : this(string.Empty, string.Empty, 0, new()) { }
}

public record ShapefileImportResultDto(
    List<ShapefileFeatureDto> Features,
    /// <summary>Nombre del CRS de origen leído del .prj, o null si el archivo no lo trae.</summary>
    string? SourceCrs,
    /// <summary>Si se aplicó reproyección. False significa que se asumió que ya venía en 4326.</summary>
    bool Reprojected,
    /// <summary>Columnas del .dbf, para poder elegir cuál es el nombre del lote.</summary>
    List<string> AttributeColumns,
    /// <summary>Columna que se usó como nombre.</summary>
    string? NameColumn,
    List<string> Warnings
)
{
    public ShapefileImportResultDto() : this(new(), null, false, new(), null, new()) { }
}
