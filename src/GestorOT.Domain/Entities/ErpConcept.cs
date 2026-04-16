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
}
