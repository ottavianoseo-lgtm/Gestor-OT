namespace GestorOT.Domain.Entities;

public class Inventory : TenantEntity, IExternalErpEntity
{
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double ReorderLevel { get; set; }
    public string? UnitA { get; set; }
    public string? UnitB { get; set; }
    public string? Unit { get; set; }
    public string? GrupoConcepto { get; set; }
    public string? SubGrupoConcepto { get; set; }
    public double ConversionFactor { get; set; } = 1;
    public string? ExternalErpId { get; set; }
}
