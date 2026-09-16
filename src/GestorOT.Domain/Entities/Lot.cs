using NetTopologySuite.Geometries;

namespace GestorOT.Domain.Entities;

public class Lot : TenantEntity, IExternalErpEntity
{
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del lote fuera de la app: la columna "lote_id" de la planilla y el
    /// atributo homónimo de los GeoJSON del relevamiento.
    ///
    /// Existe porque cruzar por nombre no alcanza: los lotes se llaman "1", "7 loma", "14 fina",
    /// se renombran entre campañas y colisionan entre campos. Con el id, reimportar actualiza el
    /// mismo lote y el GIS se vincula sin adivinar. Único por tenant cuando está cargado.
    /// </summary>
    public string? ExternalErpId { get; set; }
    public string Status { get; set; } = "Active";
    public Geometry? Geometry { get; set; }
    public decimal CadastralArea { get; set; }

    /// <summary>
    /// Cuenta del plan de centros del ERP a la que imputa este lote. Es el equivalente de
    /// Event.CodCentro en Ganadería: en agricultura el centro de costo es el lote, así que sin
    /// esto toda labor del mismo tipo imputa al mismo centro sin importar dónde se hizo.
    /// En null se hereda el de <see cref="Field.CodCentro"/>. Se puede pisar por campaña en
    /// <see cref="CampaignLot.CodCentro"/>.
    /// </summary>
    public long? CodCentro { get; set; }
    public Field? Field { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<CampaignLot> CampaignLots { get; set; } = new List<CampaignLot>();
}
