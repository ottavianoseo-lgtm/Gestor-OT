using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorOT.Mcp.Auth;

/// <summary>
/// Usa el Token fijo de la configuración o, si no hay, hace login con Email/Password
/// y cachea el JWT hasta que la API responda 401.
/// </summary>
public sealed class GestorOtLoginTokenProvider : IAccessTokenProvider
{
    public const string HttpClientName = "gestorot-auth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GestorOtOptions _options;
    private readonly ILogger<GestorOtLoginTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;

    public GestorOtLoginTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GestorOtOptions> options,
        ILogger<GestorOtLoginTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_options.Token))
            return _options.Token;

        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
            return null;

        await _lock.WaitAsync(ct);
        try
        {
            if (forceRefresh)
                _cachedToken = null;

            if (_cachedToken != null)
                return _cachedToken;

            var http = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await http.PostAsJsonAsync(
                "api/auth/login", new { email = _options.Email, password = _options.Password }, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Login contra GestorOT falló con {Status}", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>(ct);
            _cachedToken = body?.Token;
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record LoginResponse(bool Success, string? Token, string? Message);
}
