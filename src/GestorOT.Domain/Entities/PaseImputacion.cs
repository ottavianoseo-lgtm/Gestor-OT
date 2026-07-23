namespace GestorOT.Domain.Entities;

public class PaseImputacion : TenantEntity
{
    public Guid PaseLoteId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? LaborId { get; set; }

    public string IdReferencia { get; set; } = string.Empty;
    public int IdAgrupacionPase { get; set; }
    public long CodEmpresa { get; set; }
    public long CodComprobante { get; set; }
    public bool NoImputaGestion { get; set; }
    public bool NoImputaContabilidad { get; set; }
    public bool NoImputaCentro { get; set; }
    public bool NoImputaAuxiliar { get; set; }
    public int PuntoVenta { get; set; } = 1;
    public int? NumeroComprobante { get; set; }
    public DateTime Fecha { get; set; }
    public long? CodPersona { get; set; }
    public long CodMoneda { get; set; }
    public long? CodListaDePrecios { get; set; }
    public long CodConcepto { get; set; }
    public string? CodigoConcepto { get; set; }
    public decimal? CantidadAuxiliar { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Precio { get; set; }

    public long? CodPerfilImputacionDebe { get; set; }
    public long? CodPerfilImputacionHaber { get; set; }

    public long? CodCuentaDebeGestion { get; set; }
    public long? CodCuentaHaberGestion { get; set; }
    public long? CodCuentaDebeCentro { get; set; }
    public long? CodCuentaHaberCentro { get; set; }
    public long? CodCuentaDebeContabilidad { get; set; }
    public long? CodCuentaHaberContabilidad { get; set; }
    public long? CodCuentaDebeAuxiliar { get; set; }
    public long? CodCuentaHaberAuxiliar { get; set; }

    public string? Notas { get; set; }

    public PaseLote? Lote { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Labor? Labor { get; set; }
}
