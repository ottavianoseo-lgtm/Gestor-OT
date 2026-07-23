namespace GestorOT.Shared.Dtos;

public record PaseLoteDto(
    Guid Id,
    Guid TenantId,
    DateTime GeneradoEn,
    string? Descripcion,
    int TotalPases,
    string Estado,
    List<PaseImputacionDto> Pases
);

public record PaseImputacionDto(
    Guid Id,
    Guid PaseLoteId,
    Guid? WorkOrderId,
    Guid? LaborId,
    string IdReferencia,
    int IdAgrupacionPase,
    long CodEmpresa,
    long CodComprobante,
    bool NoImputaGestion,
    bool NoImputaContabilidad,
    bool NoImputaCentro,
    bool NoImputaAuxiliar,
    int PuntoVenta,
    int? NumeroComprobante,
    DateTime Fecha,
    long? CodPersona,
    long CodMoneda,
    long? CodListaDePrecios,
    long CodConcepto,
    string? CodigoConcepto,
    decimal? CantidadAuxiliar,
    decimal Cantidad,
    decimal Precio,
    long? CodPerfilImputacionDebe,
    long? CodPerfilImputacionHaber,
    long? CodCuentaDebeGestion,
    long? CodCuentaHaberGestion,
    long? CodCuentaDebeCentro,
    long? CodCuentaHaberCentro,
    long? CodCuentaDebeContabilidad,
    long? CodCuentaHaberContabilidad,
    long? CodCuentaDebeAuxiliar,
    long? CodCuentaHaberAuxiliar,
    string? Notas
);

public record GenerarLoteRequestDto(
    List<Guid>? WorkOrderIds,
    List<Guid>? LaborIds,
    string? Descripcion
);

public record PaseLoteResult(
    Guid LoteId,
    int TotalPases,
    List<string> Warnings,
    bool Success = true,
    string? Error = null
);

public record PendingImputacionItemDto(
    Guid Id,
    string Type, // "WorkOrder" or "Labor"
    string Identifier, // e.g. "OT #1024" or "Siembra Lote 4"
    DateTime Date,
    string Details,
    decimal Area,
    string Status
);

public record AccountConfigurationDto(
    Guid Id,
    Guid TenantId,
    Guid? LaborTypeId,
    string? LaborTypeName,
    string DebitAccountCode,
    string CreditAccountCode,
    string? Description,
    bool IsActive,
    long? CodEmpresa,
    long? CodComprobante,
    int PuntoVenta,
    long? CodMoneda,
    long? CodPerfilDebe,
    long? CodPerfilHaber,
    long? CodPersona,
    bool NoImputaGestion,
    bool NoImputaContabilidad,
    bool NoImputaCentro,
    bool NoImputaAuxiliar,
    long? CodCuentaDebeGestion,
    long? CodCuentaHaberGestion,
    long? CodCuentaDebeCentro,
    long? CodCuentaHaberCentro,
    long? CodCuentaDebeContabilidad,
    long? CodCuentaHaberContabilidad,
    long? CodCuentaDebeAuxiliar,
    long? CodCuentaHaberAuxiliar
);
