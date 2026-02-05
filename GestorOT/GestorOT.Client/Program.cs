using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GestorOT.Shared;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    TypeInfoResolver = AppJsonSerializerContext.Default
};
builder.Services.AddSingleton(jsonOptions);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddAntDesign();

await builder.Build().RunAsync();
