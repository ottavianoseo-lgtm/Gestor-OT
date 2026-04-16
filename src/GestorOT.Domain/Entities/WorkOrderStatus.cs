namespace GestorOT.Domain.Entities;

public class WorkOrderStatus : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#3498DB";
    public bool IsEditable { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; }
}
