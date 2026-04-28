namespace GestorOT.Domain.Entities;

public class CropStrategy : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ErpActivityId { get; set; }
    public ErpActivity? Activity { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<StrategyItem> Items { get; set; } = new List<StrategyItem>();
}
