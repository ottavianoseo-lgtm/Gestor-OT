namespace GestorOT.Mcp.Auth;

/// <summary>
/// De dónde sale el token que se manda a la API. Hoy es el login propio de GestorOT;
/// el día que se autentique contra id.gestormax.com se cambia esta implementación y
/// las tools no se enteran.
/// </summary>
public interface IAccessTokenProvider
{
    /// <param name="forceRefresh">true después de un 401: descartar el token cacheado.</param>
    Task<string?> GetTokenAsync(bool forceRefresh, CancellationToken ct);
}
