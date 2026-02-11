using GestorOT.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var tenantState = new TenantState();
builder.Services.AddSingleton(tenantState);
builder.Services.AddScoped<TenantHttpHandler>();

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<TenantHttpHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});

builder.Services.AddAntDesign();

await builder.Build().RunAsync();
