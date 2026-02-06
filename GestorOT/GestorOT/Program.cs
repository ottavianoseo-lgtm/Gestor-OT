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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
    if (dbContext != null)
    {
        try
        {
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();

            var migrationSql = new[]
            {
                @"ALTER TABLE public.""Inventories"" ADD COLUMN IF NOT EXISTS ""UnitA"" varchar(50)",
                @"ALTER TABLE public.""Inventories"" ADD COLUMN IF NOT EXISTS ""UnitB"" varchar(50)",
                @"ALTER TABLE public.""Inventories"" ADD COLUMN IF NOT EXISTS ""ConversionFactor"" numeric(18,6) DEFAULT 1",

                @"CREATE TABLE IF NOT EXISTS public.""Labors"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""WorkOrderId"" uuid REFERENCES public.""WorkOrders""(""Id"") ON DELETE CASCADE,
                    ""LotId"" uuid NOT NULL REFERENCES public.""Lots""(""Id"") ON DELETE RESTRICT,
                    ""LaborType"" varchar(100) NOT NULL,
                    ""Status"" varchar(50) NOT NULL DEFAULT 'Planned',
                    ""ExecutionDate"" timestamp,
                    ""Hectares"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE TABLE IF NOT EXISTS public.""LaborSupplies"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""LaborId"" uuid NOT NULL REFERENCES public.""Labors""(""Id"") ON DELETE CASCADE,
                    ""SupplyId"" uuid NOT NULL REFERENCES public.""Inventories""(""Id"") ON DELETE RESTRICT,
                    ""PlannedDose"" numeric(18,6) NOT NULL DEFAULT 0,
                    ""RealDose"" numeric(18,6),
                    ""PlannedTotal"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""RealTotal"" numeric(18,4),
                    ""DoseUnit"" varchar(100)
                )"
            };

            foreach (var sql in migrationSql)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }

            await conn.CloseAsync();
            Console.WriteLine("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database migration warning: {ex.Message}");
        }
    }
}

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
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GestorOT.Client._Imports).Assembly);

app.Run();
