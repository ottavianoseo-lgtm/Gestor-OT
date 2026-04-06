namespace GestorOT.Domain.Entities;

public class Inventory : TenantEntity
{
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double ReorderLevel { get; set; }
    public string? UnitA { get; set; }
    public string? UnitB { get; set; }
    public double ConversionFactor { get; set; } = 1;
}
