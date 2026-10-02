using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace GestorOT.Mcp.Auth;

public static class BearerToken
{
    public static string? FromRequest(HttpRequest? request)
    {
        var header = request?.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var token = header["Bearer ".Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    /// <summary>
    /// Vencimiento del JWT leído sin validar la firma. La firma la valida la API en cada llamada;
    /// acá solo sirve para responder 401 a tiempo y que el cliente MCP vuelva a pedir login.
    /// </summary>
    public static DateTimeOffset? GetExpiry(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }
}
