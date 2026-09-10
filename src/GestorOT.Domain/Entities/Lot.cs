using NetTopologySuite.Geometries;

namespace GestorOT.Domain.Entities;

public class Lot : TenantEntity
{
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public Geometry? Geometry { get; set; }
    public decimal CadastralArea { get; set; }

    /// <summary>
    /// Cuenta del plan de centros del ERP a la que imputa este lote. Es el equivalente de
    /// Event.CodCentro en Ganadería: en agricultura el centro de costo es el lote, así que sin
    /// esto toda labor del mismo tipo imputa al mismo centro sin importar dónde se hizo.
    /// Se puede pisar por campaña en <see cref="CampaignLot.CodCentro"/>.
    /// </summary>
    public long? CodCentro { get; set; }
    public Field? Field { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<CampaignLot> CampaignLots { get; set; } = new List<CampaignLot>();
}
