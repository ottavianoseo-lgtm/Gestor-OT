namespace GestorOT.Mcp;

/// <summary>
/// Configuración de la sección "GestorOt". Se puede pisar por variables de entorno
/// (GestorOt__BaseUrl, GestorOt__Token, ...) o user-secrets, que es lo que conviene
/// para credenciales.
/// </summary>
public sealed class GestorOtOptions
{
    public const string Section = "GestorOt";

    /// <summary>Raíz de la API. Es el único dato que cambia si la app se muda.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5159";

    /// <summary>JWT ya emitido. Si está, se usa tal cual y no se hace login.</summary>
    public string? Token { get; set; }

    /// <summary>Usuario para /api/auth/login cuando no hay Token. El MCP actúa como ese usuario.</summary>
    public string? Email { get; set; }
    public string? Password { get; set; }

    /// <summary>Solo para SuperAdmin: elige el tenant vía X-Tenant-ID.</summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Zona horaria del usuario. Las fechas que pide el modelo son días (2026-10-15) y la UI las
    /// guarda como medianoche local en UTC; el MCP hace lo mismo para que se vean igual.
    /// </summary>
    public string TimeZone { get; set; } = "America/Argentina/Buenos_Aires";

    /// <summary>Hoy en la zona del usuario.</summary>
    public DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)));

    public DateTime ToUtc(DateOnly date)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), tz);
    }
}
