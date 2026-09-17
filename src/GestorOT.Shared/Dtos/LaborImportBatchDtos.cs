namespace GestorOT.Shared.Dtos;

public record LaborImportBatchDto(
    Guid Id,
    Guid CampaignId,
    string? CampaignName,
    string FileName,
    DateTime UploadedAt,
    string? UploadedBy,
    string Status,
    int TotalRows,
    int ImportedCount,
    int PendingCount,
    int ExcludedCount
)
{
    public LaborImportBatchDto() : this(Guid.Empty, Guid.Empty, null, string.Empty, DateTime.MinValue, null, "Pending", 0, 0, 0, 0) { }
}

public record LaborImportRowStateDto(
    int RowIndex,
    string Resolution,
    Guid? ResultLaborId
);

/// <summary>
/// Vista persistente de conciliación de un lote pendiente: los mismos datos que
/// el preview de importación más el estado de resolución de cada fila.
/// </summary>
public record LaborImportBatchDetailDto(
    LaborImportBatchDto Batch,
    LaborImportPreviewDto Preview,
    List<LaborImportRowStateDto> RowStates
)
{
    public LaborImportBatchDetailDto() : this(new LaborImportBatchDto(), new LaborImportPreviewDto(), new()) { }
}

/// <summary>
/// Resultado de subir un Excel: lo verde se importó de una, el resto quedó en
/// un lote pendiente. DuplicateOfBatchId avisa si el archivo ya se había subido.
/// </summary>
public record LaborImportUploadResultDto
{
    public int LaborsCreated { get; init; }
    public int LaborsUpdated { get; init; }
    public int SuppliesCreated { get; init; }
    public int NewSuppliesCreated { get; init; }
    public int AliasesLearned { get; init; }
    public Guid? PendingBatchId { get; init; }
    public int PendingRows { get; init; }
    public Guid? DuplicateOfBatchId { get; init; }
    public string? DuplicateFileName { get; init; }
    public DateTime? DuplicateUploadedAt { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool Success { get; init; }
}

public record LaborImportBatchResolveResultDto
{
    public int Imported { get; init; }
    public int Excluded { get; init; }
    public bool BatchCompleted { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool Success { get; init; }
}

public record LaborImportBatchMappingsDto
{
    public List<LaborImportSupplyMappingDto> SupplyMappings { get; init; } = new();
    public List<LaborImportTypeMappingDto> LaborTypeMappings { get; init; } = new();
    public List<LaborImportSupplierMappingDto> SupplierMappings { get; init; } = new();
}

public record LaborImportBatchRowsRequestDto
{
    public List<int>? RowIndexes { get; init; }
}
