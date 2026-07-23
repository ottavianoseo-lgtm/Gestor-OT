namespace GestorOT.Domain.Entities;

public class AccountConfiguration : TenantEntity
{
    public Guid? LaborTypeId { get; set; }
    public string DebitAccountCode { get; set; } = string.Empty;
    public string CreditAccountCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Configuración del Pase G4
    public long? CodEmpresa { get; set; }
    public long? CodComprobante { get; set; }
    public int PuntoVenta { get; set; } = 1;
    public long? CodMoneda { get; set; }
    public long? CodPerfilDebe { get; set; }
    public long? CodPerfilHaber { get; set; }
    public long? CodPersona { get; set; }

    public bool NoImputaGestion { get; set; }
    public bool NoImputaContabilidad { get; set; }
    public bool NoImputaCentro { get; set; }
    public bool NoImputaAuxiliar { get; set; }

    public long? CodCuentaDebeGestion { get; set; }
    public long? CodCuentaHaberGestion { get; set; }
    public long? CodCuentaDebeCentro { get; set; }
    public long? CodCuentaHaberCentro { get; set; }
    public long? CodCuentaDebeContabilidad { get; set; }
    public long? CodCuentaHaberContabilidad { get; set; }
    public long? CodCuentaDebeAuxiliar { get; set; }
    public long? CodCuentaHaberAuxiliar { get; set; }

    public LaborType? LaborType { get; set; }
}
