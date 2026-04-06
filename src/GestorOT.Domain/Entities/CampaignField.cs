namespace GestorOT.Domain.Entities;

public class CampaignField : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid FieldId { get; set; }
    public decimal TargetYieldTonHa { get; set; }
    public decimal AllocatedHectares { get; set; }
    public Campaign? Campaign { get; set; }
    public Field? Field { get; set; }
}
