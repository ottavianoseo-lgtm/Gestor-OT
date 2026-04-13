namespace GestorOT.Domain.Entities;

public class CampaignLot : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid LotId { get; set; }
    public decimal ProductiveArea { get; set; }
    public Guid? CropId { get; set; }
    public Campaign? Campaign { get; set; }
    public Lot? Lot { get; set; }
}
