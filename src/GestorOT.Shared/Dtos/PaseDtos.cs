namespace GestorOT.Shared.Dtos;

public record PaseLoteDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime GeneradoEn { get; set; }
    public string? Descripcion { get; set; }
    public int TotalPases { get; set; }
    public string Estado { get; set; } = string.Empty;
    public List<PaseImputacionDto> Pases { get; set; } = new();

    public PaseLoteDto() { }
    public PaseLoteDto(
        Guid id,
        Guid tenantId,
        DateTime generadoEn,
        string? descripcion,
        int totalPases,
        string estado,
        List<PaseImputacionDto> pases)
    {
        Id = id;
        TenantId = tenantId;
        GeneradoEn = generadoEn;
        Descripcion = descripcion;
        TotalPases = totalPases;
        Estado = estado;
        Pases = pases;
    }
}

public record PaseImputacionDto
{
    public Guid Id { get; set; }
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
    public int PuntoVenta { get; set; }
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

    public PaseImputacionDto() { }
    public PaseImputacionDto(
        Guid id,
        Guid paseLoteId,
        Guid? workOrderId,
        Guid? laborId,
        string idReferencia,
        int idAgrupacionPase,
        long codEmpresa,
        long codComprobante,
        bool noImputaGestion,
        bool noImputaContabilidad,
        bool noImputaCentro,
        bool noImputaAuxiliar,
        int puntoVenta,
        int? numeroComprobante,
        DateTime fecha,
        long? codPersona,
        long codMoneda,
        long? codListaDePrecios,
        long codConcepto,
        string? codigoConcepto,
        decimal? cantidadAuxiliar,
        decimal cantidad,
        decimal precio,
        long? codPerfilImputacionDebe,
        long? codPerfilImputacionHaber,
        long? codCuentaDebeGestion,
        long? codCuentaHaberGestion,
        long? codCuentaDebeCentro,
        long? codCuentaHaberCentro,
        long? codCuentaDebeContabilidad,
        long? codCuentaHaberContabilidad,
        long? codCuentaDebeAuxiliar,
        long? codCuentaHaberAuxiliar,
        string? notas)
    {
        Id = id;
        PaseLoteId = paseLoteId;
        WorkOrderId = workOrderId;
        LaborId = laborId;
        IdReferencia = idReferencia;
        IdAgrupacionPase = idAgrupacionPase;
        CodEmpresa = codEmpresa;
        CodComprobante = codComprobante;
        NoImputaGestion = noImputaGestion;
        NoImputaContabilidad = noImputaContabilidad;
        NoImputaCentro = noImputaCentro;
        NoImputaAuxiliar = noImputaAuxiliar;
        PuntoVenta = puntoVenta;
        NumeroComprobante = numeroComprobante;
        Fecha = fecha;
        CodPersona = codPersona;
        CodMoneda = codMoneda;
        CodListaDePrecios = codListaDePrecios;
        CodConcepto = codConcepto;
        CodigoConcepto = codigoConcepto;
        CantidadAuxiliar = cantidadAuxiliar;
        Cantidad = cantidad;
        Precio = precio;
        CodPerfilImputacionDebe = codPerfilImputacionDebe;
        CodPerfilImputacionHaber = codPerfilImputacionHaber;
        CodCuentaDebeGestion = codCuentaDebeGestion;
        CodCuentaHaberGestion = codCuentaHaberGestion;
        CodCuentaDebeCentro = codCuentaDebeCentro;
        CodCuentaHaberCentro = codCuentaHaberCentro;
        CodCuentaDebeContabilidad = codCuentaDebeContabilidad;
        CodCuentaHaberContabilidad = codCuentaHaberContabilidad;
        CodCuentaDebeAuxiliar = codCuentaDebeAuxiliar;
        CodCuentaHaberAuxiliar = codCuentaHaberAuxiliar;
        Notas = notas;
    }
}

public record GenerarLoteRequestDto
{
    public List<Guid>? WorkOrderIds { get; set; }
    public List<Guid>? LaborIds { get; set; }
    public string? Descripcion { get; set; }

    public GenerarLoteRequestDto() { }
    public GenerarLoteRequestDto(List<Guid>? workOrderIds, List<Guid>? laborIds, string? descripcion)
    {
        WorkOrderIds = workOrderIds;
        LaborIds = laborIds;
        Descripcion = descripcion;
    }
}

public record PaseLoteResult
{
    public Guid LoteId { get; set; }
    public int TotalPases { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? Error { get; set; }

    public PaseLoteResult() { }
    public PaseLoteResult(Guid loteId, int totalPases, List<string> warnings, bool success = true, string? error = null)
    {
        LoteId = loteId;
        TotalPases = totalPases;
        Warnings = warnings;
        Success = success;
        Error = error;
    }
}

public record PendingImputacionItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Details { get; set; } = string.Empty;
    public decimal Area { get; set; }
    public string Status { get; set; } = string.Empty;

    public PendingImputacionItemDto() { }
    public PendingImputacionItemDto(Guid id, string type, string identifier, DateTime date, string details, decimal area, string status)
    {
        Id = id;
        Type = type;
        Identifier = identifier;
        Date = date;
        Details = details;
        Area = area;
        Status = status;
    }
}

public record AccountConfigurationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? LaborTypeId { get; set; }
    public string? LaborTypeName { get; set; }
    public string DebitAccountCode { get; set; } = string.Empty;
    public string CreditAccountCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public long? CodEmpresa { get; set; }
    public long? CodComprobante { get; set; }
    public int PuntoVenta { get; set; }
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

    public AccountConfigurationDto() { }
    public AccountConfigurationDto(
        Guid id,
        Guid tenantId,
        Guid? laborTypeId,
        string? laborTypeName,
        string debitAccountCode,
        string creditAccountCode,
        string? description,
        bool isActive,
        long? codEmpresa,
        long? codComprobante,
        int puntoVenta,
        long? codMoneda,
        long? codPerfilDebe,
        long? codPerfilHaber,
        long? codPersona,
        bool noImputaGestion,
        bool noImputaContabilidad,
        bool noImputaCentro,
        bool noImputaAuxiliar,
        long? codCuentaDebeGestion,
        long? codCuentaHaberGestion,
        long? codCuentaDebeCentro,
        long? codCuentaHaberCentro,
        long? codCuentaDebeContabilidad,
        long? codCuentaHaberContabilidad,
        long? codCuentaDebeAuxiliar,
        long? codCuentaHaberAuxiliar)
    {
        Id = id;
        TenantId = tenantId;
        LaborTypeId = laborTypeId;
        LaborTypeName = laborTypeName;
        DebitAccountCode = debitAccountCode ?? string.Empty;
        CreditAccountCode = creditAccountCode ?? string.Empty;
        Description = description;
        IsActive = isActive;
        CodEmpresa = codEmpresa;
        CodComprobante = codComprobante;
        PuntoVenta = puntoVenta;
        CodMoneda = codMoneda;
        CodPerfilDebe = codPerfilDebe;
        CodPerfilHaber = codPerfilHaber;
        CodPersona = codPersona;
        NoImputaGestion = noImputaGestion;
        NoImputaContabilidad = noImputaContabilidad;
        NoImputaCentro = noImputaCentro;
        NoImputaAuxiliar = noImputaAuxiliar;
        CodCuentaDebeGestion = codCuentaDebeGestion;
        CodCuentaHaberGestion = codCuentaHaberGestion;
        CodCuentaDebeCentro = codCuentaDebeCentro;
        CodCuentaHaberCentro = codCuentaHaberCentro;
        CodCuentaDebeContabilidad = codCuentaDebeContabilidad;
        CodCuentaHaberContabilidad = codCuentaHaberContabilidad;
        CodCuentaDebeAuxiliar = codCuentaDebeAuxiliar;
        CodCuentaHaberAuxiliar = codCuentaHaberAuxiliar;
    }
}

