using GestorOT.Api.Extensions;
using GestorOT.Api.Middleware;
using GestorOT.Client.Pages;
using GestorOT.Client;
using GestorOT.Infrastructure.Data;
using GestorOT.Client.Services; 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddAntDesign();

builder.Services.AddScoped<TenantState>();
builder.Services.AddScoped<CampaignState>();
builder.Services.AddScoped<LoadingService>();
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

var app = builder.Build();

// Auto-migrate on startup in Development / Staging only (#migrations-pipe)
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

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseStaticFiles();
app.UseRouting();
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

app.MapStaticAssets();
app.MapGroup("/api").DisableAntiforgery();
app.MapControllers();
app.MapRazorComponents<GestorOT.Api.Components.App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GestorOT.Client._Imports).Assembly);

app.Run();
