using GestorOT.Domain.Entities;

namespace GestorOT.Domain.Entities;

public class Rotation : TenantEntity
{
    public Guid CampaignLotId { get; set; }
    public Guid ErpActivityId { get; set; } // El cultivo es la actividad del ERP
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Notes { get; set; }

    public CampaignLot? CampaignLot { get; set; }
    public ErpActivity? ErpActivity { get; set; }
}
