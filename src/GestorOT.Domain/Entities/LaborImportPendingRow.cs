using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

/// <summary>
/// Fila de planilla que no hizo match al importar y espera resolución manual.
/// Guarda los datos crudos parseados más lo que se pudo auto-matchear; la
/// conciliación se re-resuelve con los mappings del batch al importar.
/// </summary>
public class LaborImportPendingRow : TenantEntity
{
    public Guid BatchId { get; set; }
    public LaborImportBatch? Batch { get; set; }
    /// <summary>Número de fila del Excel (el mismo RowIndex de la vista previa).</summary>
    public int RowIndex { get; set; }
    public DateTime? Date { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LotName { get; set; } = string.Empty;
    public Guid? LotId { get; set; }
    public Guid? CampaignLotId { get; set; }
    public decimal Hectares { get; set; }
    public string LaborTypeName { get; set; } = string.Empty;
    public Guid? LaborTypeId { get; set; }
    public string? Contractor { get; set; }
    public Guid? ContactId { get; set; }
    public string? MatchedContactName { get; set; }
    public bool IsExternalBilling { get; set; }
    /// <summary>Insumos parseados (JSON de lista de LaborImportParsedItemDto).</summary>
    public string SuppliesJson { get; set; } = "[]";
    public string ErrorsJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public LaborImportRowResolution Resolution { get; set; } = LaborImportRowResolution.Unresolved;
    public Guid? ResultLaborId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
