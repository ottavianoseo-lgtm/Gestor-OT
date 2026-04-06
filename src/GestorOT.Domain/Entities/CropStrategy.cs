namespace GestorOT.Domain.Entities;

public class CropStrategy : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string CropType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<StrategyItem> Items { get; set; } = new List<StrategyItem>();
}
