namespace GestorOT.Domain.Entities;

public class LaborTypeAlias : TenantEntity
{
    public string RawName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public Guid LaborTypeId { get; set; }
    public LaborType? LaborType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
