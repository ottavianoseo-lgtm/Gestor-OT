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
    /// <summary>
    /// Identificador estable del lote (columna 'lote_id'). Es el mismo valor que después trae
    /// el atributo homónimo de cada feature del GeoJSON, y lo que permite vincular el GIS sin
    /// cruzar por nombre.
    /// </summary>
    public string? ExternalErpId { get; set; }
    /// <summary>
    /// El lote_id ya existe con otro nombre: reimportar lo va a renombrar a lo que dice la
    /// planilla, en vez de crear un lote nuevo. Trae el nombre anterior.
    /// </summary>
    public string? RenamesLot { get; set; }
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
    /// <summary>Lotes distintos de la planilla que traen lote_id cargado.</summary>
    public int RowsWithLoteId { get; set; }
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
    /// <summary>Lotes a los que se les grabó el lote_id de la planilla.</summary>
    public int LotIdsAssigned { get; set; }
    /// <summary>Lotes que se encontraron por lote_id y cambiaron de nombre.</summary>
    public int LotsRenamed { get; set; }
    /// <summary>Lotes que se encontraron por lote_id y la planilla movió a otro campo.</summary>
    public int LotsMovedField { get; set; }
    /// <summary>Campos a los que se les asignó el centro de costo que traía la planilla.</summary>
    public int CodCentrosAssigned { get; set; }
    /// <summary>Superficie de la campaña sumada por lote, no por fila.</summary>
    public decimal TotalHectares { get; set; }
}
