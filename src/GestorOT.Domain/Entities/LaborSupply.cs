namespace GestorOT.Domain.Entities;

public class LaborSupply : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LaborId { get; set; }
    public Guid SupplyId { get; set; }
    public decimal PlannedHectares { get; set; }
    public decimal? RealHectares { get; set; }
    public decimal PlannedDose { get; set; }
    public decimal? RealDose { get; set; }
    public decimal PlannedTotal { get; set; }
    public decimal? RealTotal { get; set; }
    public decimal? CalculatedDose { get; set; }
    public decimal? CalculatedTotal { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public int TankMixOrder { get; set; }
    public bool IsSubstitute { get; set; }
    public Labor? Labor { get; set; }
    public Inventory? Supply { get; set; }
}
