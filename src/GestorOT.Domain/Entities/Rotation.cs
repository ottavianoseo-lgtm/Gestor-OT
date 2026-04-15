using GestorOT.Domain.Entities;

namespace GestorOT.Domain.Entities;

public class Rotation : TenantEntity
{
    public Guid CampaignLotId { get; set; }
    public string CropName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Notes { get; set; }
    public Guid? SuggestedLaborTypeId { get; set; }

    public CampaignLot? CampaignLot { get; set; }
    public LaborType? SuggestedLaborType { get; set; }
}
