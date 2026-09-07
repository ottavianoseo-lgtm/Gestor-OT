namespace GestorOT.Domain.Entities;

public class SupplyAlias : TenantEntity
{
    public string RawName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public Guid SupplyId { get; set; }
    public Inventory? Supply { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
