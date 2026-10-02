using GestorOT.Mcp;
using GestorOT.Mcp.Api;
using GestorOT.Mcp.Auth;
using GestorOT.Mcp.OAuth;
using Microsoft.Extensions.Options;

// Dos modos con las mismas tools:
//   (default) stdio: Claude Code / Claude Desktop lo lanzan como proceso local y hace login con
//             GestorOt:Email/Password (o GestorOt:Token).
//   --http:   servidor remoto para claude.ai. Cada usuario hace login por OAuth y el MCP reenvía
//             su JWT a la API.
// "--http" no lleva valor: se saca antes de que el parser de configuración se lo coma junto
// con el argumento siguiente (ej. --urls).
if (args.Contains("--http"))
    await RunHttpAsync(args.Where(a => a != "--http").ToArray());
else
    await RunStdioAsync(args);

static async Task RunStdioAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    // stdio usa stdout para el protocolo: cualquier log ahí rompe la sesión.
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    AddConfiguration(builder.Configuration);

    builder.Services.AddSingleton<IAccessTokenProvider, GestorOtLoginTokenProvider>();
    AddGestorOtApi(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

static async Task RunHttpAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    AddConfiguration(builder.Configuration);

    builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.Section));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<AuthorizationCodeStore>();
    builder.Services.AddSingleton<IAccessTokenProvider, IncomingRequestTokenProvider>();
    AddGestorOtApi(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .WithToolsFromAssembly();

    var app = builder.Build();

    // claude.ai solo acepta HTTPS, y con una URL mal puesta el login falla de forma muy confusa:
    // mejor que el contenedor no arranque.
    var publicUrl = app.Services.GetRequiredService<IOptions<OAuthOptions>>().Value.PublicUrl;
    if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var publicUri)
        || (publicUri.Scheme != Uri.UriSchemeHttps && !publicUri.IsLoopback))
        throw new InvalidOperationException(
            $"OAuth:PublicUrl ('{publicUrl}') tiene que ser la URL HTTPS pública del MCP (MCP_PUBLIC_URL en .env).");

    app.Use(async (context, next) =>
    {
        // La pantalla de login no se puede embeber en otra página (clickjacking).
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";

        // Sin token, con algo que no es un JWT o con el JWT vencido, se responde 401 apuntando a los metadatos OAuth:
        // así claude.ai sabe que tiene que mostrar el login.
        if (context.Request.Path.StartsWithSegments("/mcp"))
        {
            var token = BearerToken.FromRequest(context.Request);
            if (token == null || BearerToken.GetExpiry(token) is not { } exp || exp <= DateTimeOffset.UtcNow)
            {
                var metadata = context.RequestServices.GetRequiredService<IOptions<OAuthOptions>>().Value.ResourceMetadataUrl;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{metadata}\"";
                return;
            }
        }

        await next();
    });

    app.MapGet("/health", () => Results.Ok("ok"));
    app.MapOAuthEndpoints();
    app.MapMcp("/mcp");

    await app.RunAsync();
}

static void AddConfiguration(IConfigurationBuilder configuration)
{
    configuration.SetBasePath(AppContext.BaseDirectory);
    configuration.AddJsonFile("appsettings.json", optional: true);
    configuration.AddUserSecrets<Program>(optional: true);
    configuration.AddEnvironmentVariables();
}

static void AddGestorOtApi(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<GestorOtOptions>(configuration.GetSection(GestorOtOptions.Section));

    static void UseBaseUrl(IServiceProvider sp, HttpClient http)
    {
        var baseUrl = sp.GetRequiredService<IOptions<GestorOtOptions>>().Value.BaseUrl;
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    services.AddHttpClient(GestorOtLoginTokenProvider.HttpClientName, UseBaseUrl);
    services.AddTransient<BearerTokenHandler>();
    services.AddHttpClient<GestorOtApiClient>(UseBaseUrl)
        .AddHttpMessageHandler<BearerTokenHandler>();
}
