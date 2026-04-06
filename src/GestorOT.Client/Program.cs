using GestorOT.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var tenantState = new TenantState();
builder.Services.AddSingleton(tenantState);

var campaignState = new CampaignState();
builder.Services.AddSingleton(campaignState);

var loadingService = new LoadingService();
builder.Services.AddSingleton(loadingService);

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

await builder.Build().RunAsync();
