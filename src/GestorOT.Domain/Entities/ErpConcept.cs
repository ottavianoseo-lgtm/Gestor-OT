using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

public class ErpConcept : TenantEntity, IExternalErpEntity
{
    public string Description { get; set; } = string.Empty;
    public double Stock { get; set; }
    public string? UnitA { get; set; }
    public string? UnitB { get; set; }
    public string? GrupoConcepto { get; set; }
    public string? SubGrupoConcepto { get; set; }
    public string? ExternalErpId { get; set; }
    public DateTime LastSyncDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Propia o de contratista, marcado a mano desde el catálogo. Vive acá y no solo en
    /// LaborType porque hay que poder clasificarla antes de activarla (el LaborType recién
    /// se crea al activar). Si ya está activada, se mantiene en sync con
    /// LaborType.ExecutionMode; ver ErpConceptsController.SetExecutionMode.
    /// </summary>
    public LaborExecutionMode? ExecutionMode { get; set; }
}
