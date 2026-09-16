using NetTopologySuite.Geometries;

namespace GestorOT.Domain.Entities;

public class Field : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Centro de costo por defecto de todos los lotes del campo. En la operación el centro se
    /// abre por campo, no por lote, así que cargarlo acá evita repetirlo lote por lote.
    /// Lo pisan <see cref="Lot.CodCentro"/> y, dentro de una campaña, <see cref="CampaignLot.CodCentro"/>.
    /// </summary>
    public long? CodCentro { get; set; }
    public ICollection<Lot> Lots { get; set; } = new List<Lot>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
