namespace GestorOT.Shared.Dtos;

public class LotImportRowDto
{
    public int RowNumber { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LotName { get; set; } = string.Empty;
    public decimal DeclaredAreaHa { get; set; }
    public string? CropName { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsFieldNew { get; set; }
    public bool IsLotNew { get; set; }
    public bool IsCropNew { get; set; }
    public string Status { get; set; } = "Valid"; // "Valid", "Warning", "Error"
    public string? ValidationMessage { get; set; }
}

public class LotImportSummaryDto
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int WarningRows { get; set; }
    public int ErrorRows { get; set; }
    public int NewFieldsCount { get; set; }
    public int ExistingFieldsCount { get; set; }
    public int NewLotsCount { get; set; }
    public int ExistingLotsCount { get; set; }
    public decimal TotalHectares { get; set; }
    public List<string> NewCropsToCreate { get; set; } = new();
    public List<LotImportRowDto> Rows { get; set; } = new();
}

public class LotImportResultDto
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public int FieldsCreated { get; set; }
    public int LotsCreated { get; set; }
    public int CampaignLotsLinked { get; set; }
    public int RotationsCreated { get; set; }
    public int CropsCreated { get; set; }
    public decimal TotalHectares { get; set; }
}
