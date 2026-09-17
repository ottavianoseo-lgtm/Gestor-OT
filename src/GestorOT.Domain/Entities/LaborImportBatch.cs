using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

/// <summary>
/// Lote de importación de labores por Excel: lo que no hizo full match al subir
/// el archivo queda acá persistido (vista de conciliación) para matchear a mano
/// en otro momento. Lo pendiente todavía no es ninguna labor.
/// </summary>
public class LaborImportBatch : TenantEntity
{
    public Guid CampaignId { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>SHA-256 hex del archivo, para avisar si se sube el mismo dos veces.</summary>
    public string FileHash { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public LaborImportBatchStatus Status { get; set; } = LaborImportBatchStatus.Pending;
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int PendingCount { get; set; }
    public int ExcludedCount { get; set; }
    /// <summary>Decisiones de conciliación (JSON): insumos, tipos y proveedores.</summary>
    public string SupplyMappingsJson { get; set; } = "[]";
    public string LaborTypeMappingsJson { get; set; } = "[]";
    public string SupplierMappingsJson { get; set; } = "[]";

    // Navigation
    public Campaign? Campaign { get; set; }
    public ICollection<LaborImportPendingRow> Rows { get; set; } = new List<LaborImportPendingRow>();
}
