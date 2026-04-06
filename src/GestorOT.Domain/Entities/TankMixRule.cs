namespace GestorOT.Domain.Entities;

public class TankMixRule : TenantEntity
{
    public Guid ProductAId { get; set; }
    public Guid ProductBId { get; set; }
    public string Severity { get; set; } = "Warning";
    public string WarningMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Inventory? ProductA { get; set; }
    public Inventory? ProductB { get; set; }
}
