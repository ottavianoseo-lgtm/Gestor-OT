using NetTopologySuite.Geometries;

namespace GestorOT.Domain.Entities;

public class CampaignLot : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid LotId { get; set; }
    public decimal ProductiveArea { get; set; }
    public Guid? CropId { get; set; }

    /// <summary>
    /// Pisa el centro del lote solo para esta campaña. En null, se usa el del lote. Existe
    /// porque un lote puede cambiar de centro de una campaña a otra sin que su identidad
    /// cambie; si eso no pasa en la operación, se deja siempre en null.
    /// </summary>
    public long? CodCentro { get; set; }
    public Geometry? Geometry { get; set; }
    public Campaign? Campaign { get; set; }
    public Lot? Lot { get; set; }
    public ICollection<Rotation> Rotations { get; set; } = new List<Rotation>();
}
