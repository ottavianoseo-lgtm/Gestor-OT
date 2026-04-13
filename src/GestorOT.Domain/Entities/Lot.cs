using NetTopologySuite.Geometries;

namespace GestorOT.Domain.Entities;

public class Lot : TenantEntity
{
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public Geometry? Geometry { get; set; }
    public decimal CadastralArea { get; set; }
    public Field? Field { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<CampaignLot> CampaignLots { get; set; } = new List<CampaignLot>();
}
