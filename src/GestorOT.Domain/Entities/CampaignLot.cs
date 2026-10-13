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

    /// <summary>
    /// La rotacion a la que se imputa una labor de esa fecha: la vigente, y si no hay, la
    /// proxima del lote. Lo que se hace antes de que arranque el cultivo (barbecho: fumigadas,
    /// rolo) es costo del cultivo que viene, no queda sin actividad.
    /// </summary>
    public Rotation? RotacionEn(DateOnly fecha) =>
        Rotations.FirstOrDefault(r => r.StartDate <= fecha && r.EndDate >= fecha)
        ?? Rotations.Where(r => r.StartDate > fecha).OrderBy(r => r.StartDate).FirstOrDefault();
}
