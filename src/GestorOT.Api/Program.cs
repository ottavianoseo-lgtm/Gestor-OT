using GestorOT.Api.Extensions;
using GestorOT.Api.Middleware;
using GestorOT.Client.Pages;
using GestorOT.Client;
using GestorOT.Infrastructure.Data;
using GestorOT.Client.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var defaultCulture = new System.Globalization.CultureInfo("es-AR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddAntDesign();

builder.Services.AddScoped<TenantState>();
builder.Services.AddScoped<CampaignState>();
builder.Services.AddScoped<LoadingService>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddHttpClient();

// ERP Background Sync Worker
builder.Services.AddHostedService<GestorOT.Infrastructure.Services.ErpSyncWorker>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, GestorOT.Shared.AppJsonSerializerContext.Default);
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, GestorOT.Shared.AppJsonSerializerContext.Default);
});

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "GestorOT_SuperSecretKey_MultiTenancy_JWT_Token_2026!#$";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.TryGetValue("GestorOT_SessionToken", out var token) && !string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate on startup
await app.ApplyMigrationsAsync();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Solo para las rutas de la app, nunca para /api. Re-ejecutar el pipeline en /not-found hace
// que la respuesta de error de la API se reescriba: un 401 en un POST volvia como 400 de
// antiforgery (porque /not-found sí pasa por ese middleware) y en un PUT o DELETE como 405.
// El cliente no podia distinguir "falta sesión" de "pedido mal formado".
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), appBuilder =>
{
    appBuilder.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), appBuilder =>
{
    appBuilder.UseAntiforgery();
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || 
        path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
        path == "/")
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }
    await next();
});

app.MapGroup("/api").DisableAntiforgery();

// Toda la API exige sesión. Se aplica solo a los controllers y no como FallbackPolicy global
// porque el fallback alcanzaría también a los endpoints de Blazor, y entonces no se podría
// cargar ni la pantalla de login. Las excepciones van con [AllowAnonymous]: AuthController y
// los endpoints de ShareController que el contratista abre con token en vez de sesión.
app.MapControllers().RequireAuthorization();
app.MapStaticAssets();
app.MapRazorComponents<GestorOT.Api.Components.App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GestorOT.Client._Imports).Assembly);

app.Run();
