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

    public string GetEffectiveUnit()
    {
        bool IsSurface(string? u) => !string.IsNullOrWhiteSpace(u) && (
            u.Trim().Equals("ha", StringComparison.OrdinalIgnoreCase) ||
            u.Trim().Equals("hta", StringComparison.OrdinalIgnoreCase) ||
            u.Trim().Equals("has", StringComparison.OrdinalIgnoreCase) ||
            u.Trim().Equals("hectarea", StringComparison.OrdinalIgnoreCase) ||
            u.Trim().Equals("hectareas", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(UnitA) && !IsSurface(UnitA)) return UnitA.Trim();
        if (!string.IsNullOrWhiteSpace(UnitB) && !IsSurface(UnitB)) return UnitB.Trim();
        if (!string.IsNullOrWhiteSpace(Unit) && !IsSurface(Unit)) return Unit.Trim();
        return "u";
    }
}
