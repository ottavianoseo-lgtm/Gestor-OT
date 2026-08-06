using System.Text.Json.Serialization;

namespace GestorOT.Shared.Dtos;

public record ErpCompanyDto
{
    public long CodEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [JsonConstructor]
    public ErpCompanyDto() { }
    public ErpCompanyDto(long codEmpresa, string nombre)
    {
        CodEmpresa = codEmpresa;
        Nombre = nombre;
    }
}

public record ErpVoucherTypeDto
{
    public long CodComprobante { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [JsonConstructor]
    public ErpVoucherTypeDto() { }
    public ErpVoucherTypeDto(long codComprobante, string nombre)
    {
        CodComprobante = codComprobante;
        Nombre = nombre;
    }
}

public record ErpCurrencyDto
{
    public long CodMoneda { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Simbolo { get; set; }

    [JsonConstructor]
    public ErpCurrencyDto() { }
    public ErpCurrencyDto(long codMoneda, string nombre, string? simbolo = null)
    {
        CodMoneda = codMoneda;
        Nombre = nombre;
        Simbolo = simbolo;
    }
}

public record ErpProfileDto
{
    public long CodPerfil { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [JsonConstructor]
    public ErpProfileDto() { }
    public ErpProfileDto(long codPerfil, string nombre)
    {
        CodPerfil = codPerfil;
        Nombre = nombre;
    }
}

public record ErpAccountDto
{
    public long CodCuenta { get; set; }
    public string CodigoCuenta { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(CodigoCuenta) 
        ? $"{Nombre} ({CodCuenta})" 
        : $"{CodigoCuenta} - {Nombre}";

    [JsonConstructor]
    public ErpAccountDto() { }
    public ErpAccountDto(long codCuenta, string codigoCuenta, string nombre)
    {
        CodCuenta = codCuenta;
        CodigoCuenta = codigoCuenta;
        Nombre = nombre;
    }
}

public record ErpPersonItemDto
{
    public long CodPersona { get; set; }
    public string Nombre { get; set; } = string.Empty;

    [JsonConstructor]
    public ErpPersonItemDto() { }
    public ErpPersonItemDto(long codPersona, string nombre)
    {
        CodPersona = codPersona;
        Nombre = nombre;
    }
}

