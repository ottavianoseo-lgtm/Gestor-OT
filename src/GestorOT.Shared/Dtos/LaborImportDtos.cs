namespace GestorOT.Shared.Dtos;

public record LaborImportSupplyMappingDto
{
    public string RawName { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public Guid? MatchedSupplyId { get; set; }
    public string? MatchedSupplyName { get; set; }
    public double Confidence { get; set; }
    public string ConfidenceLevel { get; set; } = "None"; // "High", "Medium", "None"
    public bool IsFromAlias { get; set; }
    public string DetectedCategory { get; init; } = string.Empty;
    public string DetectedUnit { get; init; } = string.Empty;
    public int Occurrences { get; init; }
    public decimal AverageDose { get; init; }
    public string Action { get; set; } = "Match"; // "Match", "CreateNew", "Ignore"
    public string? NewItemName { get; set; }
    public string? NewCategory { get; set; }
    public string? NewUnit { get; set; }
}

public record LaborImportParsedItemDto
{
    public string SupplyName { get; init; } = string.Empty;
    public decimal Dose { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal? Total { get; init; }
    public string Category { get; init; } = string.Empty;
    public Guid? MatchedSupplyId { get; set; }
    public string? MatchedSupplyName { get; set; }
}

public record LaborImportParsedLaborDto
{
    public int RowIndex { get; init; }
    public DateTime? Date { get; init; }
    public string FieldName { get; init; } = string.Empty;
    public string LotName { get; init; } = string.Empty;
    public Guid? LotId { get; set; }
    public Guid? CampaignLotId { get; set; }
    public decimal Hectares { get; init; }
    public string LaborTypeName { get; init; } = string.Empty;
    public Guid? LaborTypeId { get; set; }
    public string? Contractor { get; init; }
    public Guid? ContactId { get; set; }
    public string? MatchedContactName { get; set; }
    public bool IsExternalBilling { get; set; }
    public string Mode { get; init; } = "Realized";
    public string Status { get; init; } = "Realized";
    public List<LaborImportParsedItemDto> Supplies { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public record LaborImportTypeMappingDto
{
    public string RawName { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public Guid? MatchedLaborTypeId { get; set; }
    public string? MatchedLaborTypeName { get; set; }
    public double Confidence { get; set; }
    public string ConfidenceLevel { get; set; } = "None"; // "High", "Medium", "None"
    public bool IsFromAlias { get; set; }
    public int Occurrences { get; init; }
    public string Action { get; set; } = "Match";
    public string? NewTypeName { get; set; }
}

public record LaborImportPreviewDto
{
    public int TotalLabors { get; init; }
    public int TotalSupplies { get; init; }
    public decimal TotalHectares { get; init; }
    public int UniqueSuppliesCount { get; init; }
    public int UnmatchedSuppliesCount { get; init; }
    public int UniqueLaborTypesCount { get; init; }
    public int UnmatchedLaborTypesCount { get; init; }
    public List<LaborImportParsedLaborDto> Labors { get; init; } = new();
    public List<LaborImportSupplyMappingDto> SupplyMappings { get; init; } = new();
    public List<LaborImportTypeMappingDto> LaborTypeMappings { get; init; } = new();
    public List<string> Diagnostics { get; init; } = new();
    public bool CanProceed { get; init; }
}

public record LaborImportExecuteRequestDto
{
    public Guid CampaignId { get; init; }
    public List<LaborImportSupplyMappingDto> SupplyMappings { get; init; } = new();
    public List<LaborImportTypeMappingDto> LaborTypeMappings { get; init; } = new();
}

public record LaborImportResultDto
{
    public int LaborsCreated { get; init; }
    public int LaborsUpdated { get; init; }
    public int SuppliesCreated { get; init; }
    public int NewSuppliesCreated { get; init; }
    public int NewLaborTypesCreated { get; init; }
    public int AliasesLearned { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool Success { get; init; }
}
