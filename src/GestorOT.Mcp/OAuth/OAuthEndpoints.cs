using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using GestorOT.Mcp.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorOT.Mcp.OAuth;

/// <summary>
/// Servidor OAuth 2.1 mínimo para que claude.ai pueda conectarse (spec de autorización de MCP):
/// metadatos (RFC 9728 / RFC 8414), registro dinámico (RFC 7591) y authorization code + PKCE.
///
/// No tiene usuarios propios: la pantalla de login le pasa usuario y contraseña a
/// POST api/auth/login de GestorOT, y el access_token que recibe claude.ai es el mismo JWT de
/// GestorOT. Por eso no hay refresh token: cuando el JWT vence (7 días), claude.ai pide login de nuevo.
/// </summary>
public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // claude.ai puede pedir la variante con el path del recurso insertado (RFC 9728 §3.1).
        app.MapGet("/.well-known/oauth-protected-resource", ProtectedResourceMetadata);
        app.MapGet("/.well-known/oauth-protected-resource/mcp", ProtectedResourceMetadata);
        app.MapGet("/.well-known/oauth-authorization-server", AuthorizationServerMetadata);
        app.MapPost("/register", Register);
        app.MapGet("/authorize", AuthorizeForm);
        app.MapPost("/authorize", AuthorizeSubmit).DisableAntiforgery();
        app.MapPost("/token", Token).DisableAntiforgery();
    }

    private static IResult ProtectedResourceMetadata(IOptions<OAuthOptions> options)
    {
        var o = options.Value;
        return Results.Json(new
        {
            resource = o.McpEndpoint,
            authorization_servers = new[] { o.PublicUrl.TrimEnd('/') },
            bearer_methods_supported = new[] { "header" },
            resource_name = "GestorOT"
        });
    }

    private static IResult AuthorizationServerMetadata(IOptions<OAuthOptions> options)
    {
        var issuer = options.Value.PublicUrl.TrimEnd('/');
        return Results.Json(new
        {
            issuer,
            authorization_endpoint = issuer + "/authorize",
            token_endpoint = issuer + "/token",
            registration_endpoint = issuer + "/register",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code" },
            code_challenge_methods_supported = new[] { "S256" },
            token_endpoint_auth_methods_supported = new[] { "none" }
        });
    }

    /// <summary>
    /// Registro dinámico sin estado: no se guarda el cliente. Se valida que los redirect_uri estén
    /// en la lista permitida y se devuelve un client_id cualquiera; /authorize vuelve a validar el
    /// redirect_uri, así que un client_id inventado no sirve para nada.
    /// </summary>
    private static async Task<IResult> Register(HttpRequest request, IOptions<OAuthOptions> options)
    {
        var body = await request.ReadFromJsonAsync<RegistrationRequest>();
        var redirectUris = body?.redirect_uris ?? new List<string>();
        if (redirectUris.Count == 0 || redirectUris.Any(u => !IsAllowedRedirect(options.Value, u)))
            return OAuthError("invalid_redirect_uri", "redirect_uri no permitido.");

        return Results.Json(new
        {
            client_id = "gestorot-" + AuthorizationCodeStore.Base64Url(RandomNumberGenerator.GetBytes(12)),
            client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            client_name = body?.client_name,
            redirect_uris = redirectUris,
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        }, statusCode: StatusCodes.Status201Created);
    }

    private static IResult AuthorizeForm(HttpRequest request, IOptions<OAuthOptions> options)
    {
        var p = AuthorizeParams.From(request.Query);
        var error = p.Validate(options.Value);
        return error != null
            ? Results.Content(Page(null, error), "text/html; charset=utf-8", statusCode: 400)
            : LoginPage(p, null);
    }

    private static async Task<IResult> AuthorizeSubmit(
        HttpRequest request,
        IOptions<OAuthOptions> options,
        IHttpClientFactory httpClientFactory,
        AuthorizationCodeStore codes,
        ILoggerFactory loggerFactory)
    {
        var form = await request.ReadFormAsync();
        var p = AuthorizeParams.From(form);
        var error = p.Validate(options.Value);
        if (error != null)
            return Results.Content(Page(null, error), "text/html; charset=utf-8", statusCode: 400);

        var email = form["email"].ToString().Trim();
        var password = form["password"].ToString();
        if (email.Length == 0 || password.Length == 0)
            return LoginPage(p, "Ingresá usuario y contraseña.", email);

        string? token;
        try
        {
            var http = httpClientFactory.CreateClient(GestorOtLoginTokenProvider.HttpClientName);
            using var response = await http.PostAsJsonAsync("api/auth/login", new { email, password });
            token = response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<LoginResponse>())?.Token
                : null;
        }
        catch (HttpRequestException ex)
        {
            loggerFactory.CreateLogger("OAuth").LogError(ex, "No se pudo llegar a GestorOT para el login");
            return LoginPage(p, "GestorOT no responde. Probá de nuevo en un rato.", email);
        }

        if (string.IsNullOrEmpty(token))
            return LoginPage(p, "Usuario o contraseña incorrectos.", email);

        var code = codes.Issue(new PendingAuthorization(p.ClientId!, p.RedirectUri!, p.CodeChallenge!, token));
        var redirect = p.RedirectUri + (p.RedirectUri!.Contains('?') ? "&" : "?")
            + "code=" + Uri.EscapeDataString(code)
            + (string.IsNullOrEmpty(p.State) ? "" : "&state=" + Uri.EscapeDataString(p.State));
        return Results.Redirect(redirect);
    }

    private static async Task<IResult> Token(HttpRequest request, AuthorizationCodeStore codes)
    {
        if (!request.HasFormContentType)
            return OAuthError("invalid_request", "Se espera application/x-www-form-urlencoded.");

        var form = await request.ReadFormAsync();
        if (form["grant_type"] != "authorization_code")
            return OAuthError("unsupported_grant_type", "Solo authorization_code.");

        var pending = codes.Redeem(form["code"].ToString());
        if (pending == null)
            return OAuthError("invalid_grant", "Código inválido o vencido.");

        if (form["redirect_uri"] != pending.RedirectUri || form["client_id"] != pending.ClientId)
            return OAuthError("invalid_grant", "redirect_uri o client_id no coinciden.");

        var verifier = form["code_verifier"].ToString();
        var challenge = AuthorizationCodeStore.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        if (verifier.Length == 0 || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(challenge), Encoding.ASCII.GetBytes(pending.CodeChallenge)))
            return OAuthError("invalid_grant", "PKCE inválido.");

        var expiresIn = BearerToken.GetExpiry(pending.AccessToken) is { } exp
            ? (long)Math.Max(0, (exp - DateTimeOffset.UtcNow).TotalSeconds)
            : 3600;

        request.HttpContext.Response.Headers.CacheControl = "no-store";
        return Results.Json(new
        {
            access_token = pending.AccessToken,
            token_type = "Bearer",
            expires_in = expiresIn
        });
    }

    private static bool IsAllowedRedirect(OAuthOptions options, string? uri) =>
        uri != null && options.AllowedRedirectUris.Contains(uri, StringComparer.Ordinal);

    private static IResult OAuthError(string error, string description) =>
        Results.Json(new { error, error_description = description }, statusCode: StatusCodes.Status400BadRequest);

    private static IResult LoginPage(AuthorizeParams p, string? error, string? email = null) =>
        Results.Content(Page(p, error, email), "text/html; charset=utf-8");

    /// <summary>Pantalla de login. Sin p (parámetros inválidos) muestra solo el error.</summary>
    private static string Page(AuthorizeParams? p, string? error, string? email = null)
    {
        static string E(string? s) => HtmlEncoder.Default.Encode(s ?? "");

        var hidden = p == null ? "" : string.Concat(
            new (string Name, string? Value)[]
            {
                ("response_type", "code"), ("client_id", p.ClientId), ("redirect_uri", p.RedirectUri),
                ("state", p.State), ("code_challenge", p.CodeChallenge), ("code_challenge_method", "S256")
            }.Select(f => $"<input type=\"hidden\" name=\"{f.Name}\" value=\"{E(f.Value)}\">"));

        var form = p == null ? "" : $"""
            <form method="post" action="authorize">
              {hidden}
              <label>Usuario<input name="email" type="email" autocomplete="username" required autofocus value="{E(email)}"></label>
              <label>Contraseña<input name="password" type="password" autocomplete="current-password" required></label>
              <button type="submit">Ingresar y permitir acceso</button>
            </form>
            <p class="hint">Claude va a poder ver y cargar campañas, lotes, labores y órdenes de trabajo con tus permisos de GestorOT.</p>
            """;

        return $$"""
            <!doctype html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>GestorOT · Conectar con Claude</title>
            <style>
              :root { color-scheme: light dark; --bg:#f4f5f7; --card:#fff; --fg:#1d2329; --muted:#5f6b76; --accent:#2f7d32; --err:#b3261e; --line:#d6dbe0; }
              @media (prefers-color-scheme: dark) { :root { --bg:#14171a; --card:#1e2226; --fg:#e8eaed; --muted:#9aa4ad; --accent:#5fb663; --err:#f2b8b5; --line:#343a40; } }
              body { margin:0; min-height:100vh; display:grid; place-items:center; background:var(--bg); color:var(--fg); font:15px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif; }
              main { width:min(360px, calc(100vw - 32px)); background:var(--card); border:1px solid var(--line); border-radius:12px; padding:28px; }
              h1 { font-size:20px; margin:0 0 4px; } .sub { color:var(--muted); margin:0 0 20px; }
              label { display:block; font-weight:600; margin-bottom:14px; }
              input:not([type=hidden]) { display:block; width:100%; box-sizing:border-box; margin-top:6px; padding:10px 12px; font:inherit; color:inherit; background:transparent; border:1px solid var(--line); border-radius:8px; }
              button { width:100%; padding:11px; font:inherit; font-weight:600; color:#fff; background:var(--accent); border:0; border-radius:8px; cursor:pointer; }
              .error { color:var(--err); margin:0 0 16px; } .hint { color:var(--muted); font-size:13px; margin:16px 0 0; }
            </style></head>
            <body><main>
              <h1>GestorOT</h1><p class="sub">Conectar con Claude</p>
              {{(error == null ? "" : $"<p class=\"error\">{E(error)}</p>")}}
              {{form}}
            </main></body></html>
            """;
    }

    private sealed record RegistrationRequest(List<string>? redirect_uris, string? client_name);

    private sealed record LoginResponse(bool Success, string? Token);

    private sealed record AuthorizeParams(
        string? ResponseType, string? ClientId, string? RedirectUri, string? State,
        string? CodeChallenge, string? CodeChallengeMethod)
    {
        public static AuthorizeParams From(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> values)
        {
            var d = values.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            string? Get(string k) => d.TryGetValue(k, out var v) && v.Length > 0 ? v : null;
            return new(Get("response_type"), Get("client_id"), Get("redirect_uri"), Get("state"),
                Get("code_challenge"), Get("code_challenge_method"));
        }

        public string? Validate(OAuthOptions options)
        {
            if (!IsAllowedRedirect(options, RedirectUri)) return "La aplicación que pidió el acceso no está permitida.";
            if (ResponseType != "code") return "response_type no soportado.";
            if (ClientId == null) return "Falta client_id.";
            if (CodeChallenge == null || CodeChallengeMethod != "S256") return "Se requiere PKCE (S256).";
            return null;
        }
    }
}
