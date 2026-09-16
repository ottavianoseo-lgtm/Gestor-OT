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
    /// <summary>La fila trae una geometría GIS válida (GeoJSON o WKT) para el lote.</summary>
    public bool HasGeometry { get; set; }
    /// <summary>
    /// La geometría venía topológicamente inválida y se normalizó para poder usarla. Entra
    /// igual, pero conviene contrastarla contra la superficie declarada.
    /// </summary>
    public bool GeometryRepaired { get; set; }
    /// <summary>Centro de costo del ERP que la planilla asigna al campo de esta fila.</summary>
    public long? CodCentro { get; set; }
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
    public int GeometryRows { get; set; }
    /// <summary>Filas cuya geometría hubo que normalizar para poder importarla.</summary>
    public int RepairedGeometryRows { get; set; }
    /// <summary>Campos de la planilla que traen centro de costo del ERP.</summary>
    public int FieldsWithCodCentro { get; set; }
    /// <summary>
    /// Superficie de la campaña sumada por lote, no por fila: un lote con dos cultivos ocupa
    /// dos filas pero aporta sus hectáreas una sola vez.
    /// </summary>
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
    /// <summary>Lotes a los que se les guardó la geometría GIS en esta importación.</summary>
    public int GeometriesImported { get; set; }
    /// <summary>Campos a los que se les asignó el centro de costo que traía la planilla.</summary>
    public int CodCentrosAssigned { get; set; }
    /// <summary>Superficie de la campaña sumada por lote, no por fila.</summary>
    public decimal TotalHectares { get; set; }
}
