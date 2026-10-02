namespace GestorOT.Mcp.OAuth;

/// <summary>Sección "OAuth". Solo se usa en modo HTTP (--http).</summary>
public sealed class OAuthOptions
{
    public const string Section = "OAuth";

    /// <summary>
    /// URL pública del MCP tal como la ve claude.ai (detrás del reverse proxy), sin barra final.
    /// Es el issuer y el "resource" de los metadatos OAuth; el endpoint MCP queda en {PublicUrl}/mcp.
    /// </summary>
    public string PublicUrl { get; set; } = "http://localhost:8090";

    /// <summary>
    /// Únicos redirect_uri aceptados. Como el registro dinámico no guarda nada, esta lista es lo que
    /// impide que un tercero se lleve el código de autorización a otro lado.
    /// </summary>
    public List<string> AllowedRedirectUris { get; set; } = new()
    {
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback"
    };

    public string McpEndpoint => PublicUrl.TrimEnd('/') + "/mcp";
    public string ResourceMetadataUrl => PublicUrl.TrimEnd('/') + "/.well-known/oauth-protected-resource";
}
