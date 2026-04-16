using NetTopologySuite.Geometries;

namespace GestorOT.Domain.Entities;

public class Field : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<Lot> Lots { get; set; } = new List<Lot>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
