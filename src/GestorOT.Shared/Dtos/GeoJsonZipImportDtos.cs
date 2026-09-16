namespace GestorOT.Shared.Dtos;

public record GeoJsonZipFeaturePreviewDto(
    string LoteId,
    string FileName,
    Guid? LotId,
    string? LotName,
    Guid? FieldId,
    string? FieldName,
    bool LotFound,
    string Wkt,
    decimal GisAreaHa,
    decimal? DeclaredAreaHa,
    decimal? DifferenceHa,
    double? DeviationPercent,
    bool HasExistingGeometry,
    string? Warning = null
)
{
    public GeoJsonZipFeaturePreviewDto() : this(string.Empty, string.Empty, null, null, null, null, false, string.Empty, 0, null, null, null, false) { }
}

public record GeoJsonZipPreviewResultDto(
    int TotalInZip,
    int MatchedCount,
    int UnmatchedCount,
    List<string> FieldsCovered,
    int WarningsCount,
    List<GeoJsonZipFeaturePreviewDto> Items
)
{
    public GeoJsonZipPreviewResultDto() : this(0, 0, 0, new(), 0, new()) { }
}

public record GeoJsonZipApplyItemDto(
    Guid LotId,
    string Wkt,
    decimal GisAreaHa
)
{
    public GeoJsonZipApplyItemDto() : this(Guid.Empty, string.Empty, 0) { }
}

public record GeoJsonZipApplyRequestDto(
    List<GeoJsonZipApplyItemDto> Items,
    Guid? CampaignId = null
)
{
    // Los argumentos van tipados y completos a proposito: con `this(new())` el target-typed
    // `new()` resolvia al constructor de copia que todo record sintetiza (el compilador
    // prefiere la sobrecarga que no omite parametros opcionales), asi que este constructor
    // se llamaba a si mismo. Al deserializar el body del apply eso era un StackOverflow, que
    // no se puede atrapar: mataba el proceso y el proxy devolvia 502 en vez de una respuesta.
    public GeoJsonZipApplyRequestDto() : this(new List<GeoJsonZipApplyItemDto>(), null) { }
}

public record GeoJsonZipApplyResultDto(
    bool Success,
    int LinkedCount,
    List<string> Warnings,
    string? Error = null
)
{
    public GeoJsonZipApplyResultDto() : this(false, 0, new()) { }
}
