using GestorOT.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var tenantState = new TenantState();
builder.Services.AddSingleton(tenantState);

var campaignState = new CampaignState();
builder.Services.AddSingleton(campaignState);

builder.Services.AddScoped<TenantHttpHandler>();
builder.Services.AddScoped<CampaignHttpHandler>();

builder.Services.AddScoped(sp =>
{
    var tenantHandler = sp.GetRequiredService<TenantHttpHandler>();
    var campaignHandler = sp.GetRequiredService<CampaignHttpHandler>();
    campaignHandler.InnerHandler = new HttpClientHandler();
    tenantHandler.InnerHandler = campaignHandler;
    return new HttpClient(tenantHandler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});

builder.Services.AddScoped<ContextState>();

builder.Services.AddAntDesign();

await builder.Build().RunAsync();
