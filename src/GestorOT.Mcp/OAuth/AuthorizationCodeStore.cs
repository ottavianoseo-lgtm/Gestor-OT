using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace GestorOT.Mcp.OAuth;

/// <summary>
/// Códigos de autorización en memoria: viven 5 minutos y se usan una sola vez. Si el contenedor
/// se reinicia en medio de un login, el usuario solo tiene que volver a conectar.
/// </summary>
public sealed class AuthorizationCodeStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly IMemoryCache _cache;

    public AuthorizationCodeStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Issue(PendingAuthorization authorization)
    {
        var code = Base64Url(RandomNumberGenerator.GetBytes(32));
        _cache.Set(Key(code), authorization, Lifetime);
        return code;
    }

    public PendingAuthorization? Redeem(string code)
    {
        var key = Key(code);
        if (!_cache.TryGetValue(key, out PendingAuthorization? authorization))
            return null;
        _cache.Remove(key);
        return authorization;
    }

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Key(string code) => "oauth-code:" + code;
}

/// <param name="AccessToken">JWT de GestorOT obtenido con el login del usuario.</param>
public sealed record PendingAuthorization(
    string ClientId, string RedirectUri, string CodeChallenge, string AccessToken);
