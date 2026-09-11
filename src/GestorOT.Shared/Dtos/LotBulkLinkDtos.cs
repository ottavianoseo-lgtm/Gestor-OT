namespace GestorOT.Shared.Dtos;

public enum LotMatchStatus
{
    /// <summary>Un único lote del campo con ese nombre.</summary>
    ExactMatch = 0,
    /// <summary>Más de un candidato: lo tiene que resolver el operador.</summary>
    Ambiguous = 1,
    /// <summary>Ningún lote con ese nombre en el campo.</summary>
    NoMatch = 2
}

public enum LotLinkAction
{
    /// <summary>Vincular la geometría a un lote existente.</summary>
    Link = 0,
    /// <summary>Crear un lote nuevo con esa geometría.</summary>
    Create = 1,
    /// <summary>No hacer nada con esta feature.</summary>
    Skip = 2
}

public record LotCandidateDto(Guid LotId, string Name, bool HasGeometry)
{
    public LotCandidateDto() : this(Guid.Empty, string.Empty, false) { }
}

/// <summary>Qué se propone hacer con un polígono importado.</summary>
public record LotMatchProposalDto(
    string FeatureName,
    string Wkt,
    double AreaHa,
    string? SourceShapefile,
    LotMatchStatus Status,
    LotLinkAction SuggestedAction,
    Guid? MatchedLotId,
    string? MatchedLotName,
    /// <summary>El lote destino ya tiene polígono: hay que decidir reemplazar o combinar.</summary>
    bool TargetHasGeometry,
    List<LotCandidateDto> Candidates
)
{
    public LotMatchProposalDto() : this(string.Empty, string.Empty, 0, null, LotMatchStatus.NoMatch, LotLinkAction.Skip, null, null, false, new()) { }
}

/// <summary>
/// El matching se pide siempre acotado a un campo: los .dbf reales traen el lote como "1", "2",
/// sin prefijo del establecimiento, así que cruzar por nombre a nivel global colisiona entre campos.
/// </summary>
public record LotMatchRequestDto(Guid FieldId, List<ShapefileFeatureDto> Features)
{
    public LotMatchRequestDto() : this(Guid.Empty, new()) { }
}

public record LotMatchResultDto(
    List<LotMatchProposalDto> Proposals,
    int ToLink,
    int ToCreate,
    int Ambiguous
)
{
    public LotMatchResultDto() : this(new(), 0, 0, 0) { }
}

public record LotBulkLinkItemDto(
    string FeatureName,
    string Wkt,
    LotLinkAction Action,
    /// <summary>Destino cuando la acción es Link.</summary>
    Guid? LotId = null,
    /// <summary>Nombre del lote a crear cuando la acción es Create. Si viene vacío se usa FeatureName.</summary>
    string? NewLotName = null,
    long? CodCentro = null
)
{
    public LotBulkLinkItemDto() : this(string.Empty, string.Empty, LotLinkAction.Skip) { }
}

public record LotBulkLinkRequestDto(
    Guid FieldId,
    List<LotBulkLinkItemDto> Items,
    Guid? CampaignId = null,
    /// <summary>Unir con la geometría que el lote ya tenga, en vez de reemplazarla.</summary>
    bool CombineGeometry = false,
    /// <summary>Aplicar aunque haya solapamientos. Sin esto, los solapados se rechazan.</summary>
    bool OverrideOverlap = false
)
{
    public LotBulkLinkRequestDto() : this(Guid.Empty, new()) { }
}

public record LotBulkLinkItemResultDto(
    string FeatureName,
    /// <summary>Created, Updated, Skipped o Rejected.</summary>
    string Outcome,
    Guid? LotId,
    string? Message
)
{
    public LotBulkLinkItemResultDto() : this(string.Empty, "Skipped", null, null) { }
}

public record LotBulkLinkResultDto(
    bool Success,
    int Created,
    int Updated,
    int Skipped,
    int Rejected,
    List<LotBulkLinkItemResultDto> Items,
    /// <summary>Solapamientos detectados, agrupados en una sola respuesta en vez de un modal por lote.</summary>
    List<string> OverlapWarnings
)
{
    public LotBulkLinkResultDto() : this(false, 0, 0, 0, 0, new(), new()) { }
}
