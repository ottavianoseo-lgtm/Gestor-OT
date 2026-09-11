using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

public class AccountConfiguration : TenantEntity
{
    public Guid? LaborTypeId { get; set; }

    /// <summary>
    /// Acota la regla a una actividad del ERP (el cultivo o imputación de la labor). Permite
    /// que la misma tarea impute distinto según sobre qué se hizo. En null aplica a todas.
    /// </summary>
    public Guid? ErpActivityId { get; set; }

    /// <summary>
    /// Acota la regla a labores propias o de contratista. Es la razón habitual de que la misma
    /// tarea se impute distinto, así que resolverla acá evita tener que marcar labor por labor.
    /// En null, la regla aplica a los dos modos.
    /// </summary>
    public LaborExecutionMode? ExecutionMode { get; set; }
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
    public ErpActivity? ErpActivity { get; set; }
}
