using GestorOT.Client.Pages;
using GestorOT.Components;
using GestorOT.Data;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentTenantService>();

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
                )",

                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""AgreedRate"" numeric(18,4) DEFAULT 0",

                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""EffectiveArea"" numeric(18,4) DEFAULT 0",

                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""Rate"" numeric(18,4) DEFAULT 0",

                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""RateUnit"" varchar(50) DEFAULT 'ha'",

                @"CREATE TABLE IF NOT EXISTS public.""CropStrategies"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""Name"" varchar(200) NOT NULL,
                    ""CropType"" varchar(100),
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE TABLE IF NOT EXISTS public.""StrategyItems"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""CropStrategyId"" uuid NOT NULL REFERENCES public.""CropStrategies""(""Id"") ON DELETE CASCADE,
                    ""LaborType"" varchar(100) NOT NULL,
                    ""DayOffset"" integer NOT NULL DEFAULT 0,
                    ""DefaultSupplies"" jsonb
                )",

                @"CREATE TABLE IF NOT EXISTS public.""ServiceSettlements"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""WorkOrderId"" uuid NOT NULL REFERENCES public.""WorkOrders""(""Id"") ON DELETE CASCADE,
                    ""TotalHectares"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""AgreedRate"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""TotalAmount"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""GeneratedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""ErpSyncStatus"" varchar(50) DEFAULT 'Pending'
                )",

                @"CREATE TABLE IF NOT EXISTS public.""Tenants"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""Name"" varchar(200) NOT NULL,
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"ALTER TABLE public.""Fields"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",
                @"ALTER TABLE public.""Lots"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",
                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",
                @"ALTER TABLE public.""Inventories"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",
                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",
                @"ALTER TABLE public.""CropStrategies"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",
                @"ALTER TABLE public.""ServiceSettlements"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid DEFAULT '00000000-0000-0000-0000-000000000000'",

                @"CREATE TABLE IF NOT EXISTS public.""SharedTokens"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""WorkOrderId"" uuid NOT NULL REFERENCES public.""WorkOrders""(""Id"") ON DELETE CASCADE,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""TokenHash"" varchar(128) NOT NULL,
                    ""ExpiresAt"" timestamp NOT NULL,
                    ""IsRevoked"" boolean NOT NULL DEFAULT false,
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SharedTokens_TokenHash"" ON public.""SharedTokens"" (""TokenHash"")"
            };

            foreach (var sql in migrationSql)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }

            using var seedCmd = conn.CreateCommand();
            seedCmd.CommandText = @"
                INSERT INTO public.""Tenants"" (""Id"", ""Name"", ""CreatedAt"")
                VALUES ('11111111-1111-1111-1111-111111111111', 'Empresa A', CURRENT_TIMESTAMP)
                ON CONFLICT (""Id"") DO NOTHING;
                INSERT INTO public.""Tenants"" (""Id"", ""Name"", ""CreatedAt"")
                VALUES ('22222222-2222-2222-2222-222222222222', 'Empresa B', CURRENT_TIMESTAMP)
                ON CONFLICT (""Id"") DO NOTHING;
                UPDATE public.""Fields"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE public.""Lots"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE public.""WorkOrders"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE public.""Inventories"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE public.""Labors"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE public.""CropStrategies"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
                UPDATE public.""ServiceSettlements"" SET ""TenantId"" = '11111111-1111-1111-1111-111111111111' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
            ";
            await seedCmd.ExecuteNonQueryAsync();

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
