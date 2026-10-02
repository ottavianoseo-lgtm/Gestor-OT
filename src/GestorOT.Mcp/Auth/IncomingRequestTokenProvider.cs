using Microsoft.AspNetCore.Http;

namespace GestorOT.Mcp.Auth;

/// <summary>
/// Modo HTTP: el token es el que mandó el cliente MCP (claude.ai) en su request, que a su vez
/// es el JWT de GestorOT que entregó el flujo OAuth. Se reenvía tal cual; la API lo valida.
/// </summary>
public sealed class IncomingRequestTokenProvider : IAccessTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IncomingRequestTokenProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<string?> GetTokenAsync(bool forceRefresh, CancellationToken ct) =>
        Task.FromResult(BearerToken.FromRequest(_httpContextAccessor.HttpContext?.Request));
}
