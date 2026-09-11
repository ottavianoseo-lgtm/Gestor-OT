using GestorOT.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<TenantState>();
builder.Services.AddScoped<CampaignState>();
builder.Services.AddScoped<LoadingService>();
builder.Services.AddScoped<AuthState>();

builder.Services.AddScoped<DashboardState>();
builder.Services.AddScoped<CatalogCache>();

builder.Services.AddScoped<TenantHttpHandler>();
builder.Services.AddScoped<CampaignHttpHandler>();
builder.Services.AddScoped<ErrorHandlingHttpHandler>();

builder.Services.AddScoped(sp =>
{
    var tenantHandler = sp.GetRequiredService<TenantHttpHandler>();
    var campaignHandler = sp.GetRequiredService<CampaignHttpHandler>();
    var errorHandler = sp.GetRequiredService<ErrorHandlingHttpHandler>();
    campaignHandler.InnerHandler = new HttpClientHandler();
    tenantHandler.InnerHandler = campaignHandler;
    errorHandler.InnerHandler = tenantHandler;
    return new HttpClient(errorHandler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});

builder.Services.AddAntDesign();

var host = builder.Build();

// La empresa elegida se recupera acá y no desde un componente: si alguna página alcanzara a
// pedirle datos a la API antes, la request saldría sin X-Tenant-ID y traería las de todas.
await host.Services.GetRequiredService<TenantState>().RestoreAsync();

await host.RunAsync();
