namespace GestorOT.Domain.Entities;

public class StrategyItem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CropStrategyId { get; set; }
    public Guid LaborTypeId { get; set; }
    public Guid? ErpActivityId { get; set; }
    public int DayOffset { get; set; }
    public string? DefaultSuppliesJson { get; set; }
    public CropStrategy? Strategy { get; set; }
}
