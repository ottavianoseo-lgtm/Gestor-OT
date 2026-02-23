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

    builder.Services.AddScoped<GestorOT.Services.TenantSessionInterceptor>();

    builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
    {
        options.UseNpgsql(dataSource, npgsqlOptions =>
        {
            npgsqlOptions.UseNetTopologySuite();
        });

        var tenantInterceptor = serviceProvider.GetRequiredService<GestorOT.Services.TenantSessionInterceptor>();
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        options.AddInterceptors(
            tenantInterceptor,
            new GestorOT.Services.AuditInterceptor(httpContextAccessor),
            new GestorOT.Services.CampaignLockedInterceptor());
        
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
builder.Services.AddScoped<GestorOT.Services.CampaignContextService>();
builder.Services.AddScoped<GestorOT.Services.AgronomicValidationService>();
builder.Services.AddScoped<GestorOT.Services.AuditInterceptor>();
builder.Services.AddScoped<GestorOT.Services.StockValidatorService>();
builder.Services.AddScoped<GestorOT.Services.IsoXmlExporterService>();
builder.Services.AddScoped<GestorOT.Services.CampaignManagerService>();
builder.Services.AddSingleton<GestorOT.Shared.Services.ITenantService, GestorOT.Services.MockTenantService>();

builder.Services.AddValidation();

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

                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SharedTokens_TokenHash"" ON public.""SharedTokens"" (""TokenHash"")",

                @"CREATE TABLE IF NOT EXISTS public.""UserProfiles"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""Email"" varchar(200) NOT NULL,
                    ""DisplayName"" varchar(200) NOT NULL,
                    ""Role"" varchar(50) NOT NULL DEFAULT 'Agronomist',
                    ""IsActive"" boolean NOT NULL DEFAULT true,
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE TABLE IF NOT EXISTS public.""TankMixRules"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""ProductAId"" uuid NOT NULL REFERENCES public.""Inventories""(""Id"") ON DELETE RESTRICT,
                    ""ProductBId"" uuid NOT NULL REFERENCES public.""Inventories""(""Id"") ON DELETE RESTRICT,
                    ""Severity"" varchar(50) NOT NULL DEFAULT 'Warning',
                    ""WarningMessage"" varchar(500) NOT NULL,
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE TABLE IF NOT EXISTS public.""AuditLogs"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""UserId"" varchar(200),
                    ""UserEmail"" varchar(200),
                    ""Action"" varchar(100) NOT NULL,
                    ""EntityType"" varchar(100) NOT NULL,
                    ""EntityId"" varchar(100),
                    ""OldValue"" text,
                    ""NewValue"" text,
                    ""Timestamp"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_Timestamp"" ON public.""AuditLogs"" (""Timestamp"" DESC)",
                @"CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_EntityType"" ON public.""AuditLogs"" (""EntityType"")",

                @"CREATE TABLE IF NOT EXISTS public.""Campaigns"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""Name"" varchar(200) NOT NULL,
                    ""StartDate"" date NOT NULL,
                    ""EndDate"" date,
                    ""IsActive"" boolean NOT NULL DEFAULT true,
                    ""Status"" varchar(50) NOT NULL DEFAULT 'Planning',
                    ""BudgetTotalUSD"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""BusinessRules"" jsonb,
                    ""CreatedAt"" timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP
                )",

                @"CREATE TABLE IF NOT EXISTS public.""CampaignFields"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""CampaignId"" uuid NOT NULL REFERENCES public.""Campaigns""(""Id"") ON DELETE CASCADE,
                    ""FieldId"" uuid NOT NULL REFERENCES public.""Fields""(""Id"") ON DELETE CASCADE,
                    ""TargetYieldTonHa"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""AllocatedHectares"" numeric(18,4) NOT NULL DEFAULT 0
                )",

                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CampaignFields_CampaignId_FieldId"" ON public.""CampaignFields"" (""CampaignId"", ""FieldId"")",

                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""CampaignId"" uuid REFERENCES public.""Campaigns""(""Id"") ON DELETE SET NULL",

                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""OTNumber"" text NOT NULL DEFAULT ''",
                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""ContractorId"" uuid",
                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""PlannedDate"" timestamp NOT NULL DEFAULT NOW()",
                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""ExpirationDate"" timestamp NOT NULL DEFAULT NOW()",
                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""EstimatedCostUSD"" numeric NOT NULL DEFAULT 0",
                @"ALTER TABLE public.""WorkOrders"" ADD COLUMN IF NOT EXISTS ""StockReserved"" boolean NOT NULL DEFAULT false",

                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""PrescriptionMapUrl"" text",
                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""MachineryUsedId"" text",
                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""WeatherLogJson"" text",
                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""EvidencePhotosJson"" text",

                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""Notes"" text",
                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""MetadataExterna"" jsonb",

                @"ALTER TABLE public.""LaborSupplies"" ADD COLUMN IF NOT EXISTS ""TankMixOrder"" integer NOT NULL DEFAULT 0",
                @"ALTER TABLE public.""LaborSupplies"" ADD COLUMN IF NOT EXISTS ""IsSubstitute"" boolean NOT NULL DEFAULT false",

                @"ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""PlannedDate"" timestamp",

                @"ALTER TABLE public.""Lots"" ADD COLUMN IF NOT EXISTS ""CadastralArea"" numeric(18,4) NOT NULL DEFAULT 0",

                @"CREATE TABLE IF NOT EXISTS public.""CampaignLots"" (
                    ""Id"" uuid PRIMARY KEY,
                    ""TenantId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    ""CampaignId"" uuid NOT NULL REFERENCES public.""Campaigns""(""Id"") ON DELETE CASCADE,
                    ""LotId"" uuid NOT NULL REFERENCES public.""Lots""(""Id"") ON DELETE CASCADE,
                    ""ProductiveArea"" numeric(18,4) NOT NULL DEFAULT 0,
                    ""CropId"" uuid
                )",

                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CampaignLots_CampaignId_LotId"" ON public.""CampaignLots"" (""CampaignId"", ""LotId"")",

                @"DO $$ BEGIN
                    ALTER TABLE public.""CampaignLots"" ENABLE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON public.""CampaignLots"";
                    CREATE POLICY tenant_isolation ON public.""CampaignLots""
                        USING (
                            NULLIF(current_setting('app.current_tenant', true), '') IS NULL
                            OR ""TenantId"" = current_setting('app.current_tenant', true)::uuid
                        );
                EXCEPTION WHEN OTHERS THEN NULL;
                END $$",

                @"ALTER TABLE public.""Labors"" DROP CONSTRAINT IF EXISTS ""FK_Labors_WorkOrders_WorkOrderId""",
                @"ALTER TABLE public.""Labors"" ADD CONSTRAINT ""FK_Labors_WorkOrders_WorkOrderId""
                    FOREIGN KEY (""WorkOrderId"") REFERENCES public.""WorkOrders""(""Id"") ON DELETE SET NULL",

                @"DO $$ BEGIN
                    ALTER TABLE public.""Campaigns"" ENABLE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON public.""Campaigns"";
                    CREATE POLICY tenant_isolation ON public.""Campaigns""
                        USING (
                            NULLIF(current_setting('app.current_tenant', true), '') IS NULL
                            OR ""TenantId"" = current_setting('app.current_tenant', true)::uuid
                        );
                EXCEPTION WHEN OTHERS THEN NULL;
                END $$",

                @"DO $$ BEGIN
                    ALTER TABLE public.""CampaignFields"" ENABLE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON public.""CampaignFields"";
                    CREATE POLICY tenant_isolation ON public.""CampaignFields""
                        USING (
                            NULLIF(current_setting('app.current_tenant', true), '') IS NULL
                            OR ""TenantId"" = current_setting('app.current_tenant', true)::uuid
                        );
                EXCEPTION WHEN OTHERS THEN NULL;
                END $$"
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
