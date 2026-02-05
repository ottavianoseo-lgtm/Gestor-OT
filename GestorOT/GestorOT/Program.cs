using GestorOT.Client.Pages;
using GestorOT.Components;
using GestorOT.Data;
using GestorOT.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(connectionString))
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseNetTopologySuite();
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(dataSource, npgsqlOptions =>
        {
            npgsqlOptions.UseNetTopologySuite();
        });
        
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.LogTo(Console.WriteLine);
        }
    });
}
else
{
    Console.WriteLine("WARNING: No database connection string configured. Database features disabled.");
    Console.WriteLine("Set SUPABASE_CONNECTION_STRING environment variable to enable database features.");
}

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddAntDesign();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GestorOT.Client._Imports).Assembly);

app.Run();
