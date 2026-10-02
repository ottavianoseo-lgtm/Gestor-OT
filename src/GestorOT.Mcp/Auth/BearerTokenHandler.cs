using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace GestorOT.Mcp.Auth;

/// <summary>
/// Agrega el bearer (y X-Tenant-ID si está configurado) a cada request. Ante un 401
/// pide un token nuevo y reintenta una sola vez.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IAccessTokenProvider _tokens;
    private readonly GestorOtOptions _options;

    public BearerTokenHandler(IAccessTokenProvider tokens, IOptions<GestorOtOptions> options)
    {
        _tokens = tokens;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_options.TenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-ID", _options.TenantId);

        var token = await SetTokenAsync(request, forceRefresh: false, ct);
        var response = await base.SendAsync(request, ct);

        // Solo tiene sentido reintentar requests sin cuerpo (GET).
        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Content != null)
            return response;

        var refreshed = await _tokens.GetTokenAsync(forceRefresh: true, ct);
        if (refreshed == null || refreshed == token)
            return response;

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(request, ct);
    }

    private async Task<string?> SetTokenAsync(HttpRequestMessage request, bool forceRefresh, CancellationToken ct)
    {
        var token = await _tokens.GetTokenAsync(forceRefresh, ct);
        request.Headers.Authorization = token != null ? new AuthenticationHeaderValue("Bearer", token) : null;
        return token;
    }
}
