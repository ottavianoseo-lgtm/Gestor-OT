using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Interceptors;
using GestorOT.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;

namespace GestorOT.Api.Extensions;

public static class ServiceExtensions
{
    public static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ICampaignContextService, CampaignContextService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
    }

    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING") 
            ?? configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString))
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseNetTopologySuite();
            dataSourceBuilder.ConnectionStringBuilder.KeepAlive = 30;
            dataSourceBuilder.ConnectionStringBuilder.TcpKeepAlive = true;
            // MinPoolSize prevents Supabase from destroying all pool connections
            dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 1;

            var dataSource = dataSourceBuilder.Build();

            services.AddScoped<TenantSessionInterceptor>();

            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(dataSource, npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                    npgsqlOptions.CommandTimeout(300);
                });

                options.ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning,
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

                var tenantInterceptor = serviceProvider.GetRequiredService<TenantSessionInterceptor>();
                var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
                options.AddInterceptors(
                    tenantInterceptor,
                    new AuditInterceptor(httpContextAccessor),
                    new CampaignLockedInterceptor());
                
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    options.EnableSensitiveDataLogging();
                }
            });

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddScoped<IAgronomicValidationService, AgronomicValidationService>();
            services.AddScoped<IStockValidatorService, StockValidatorService>();
            services.AddScoped<IIsoXmlExporterService, IsoXmlExporterService>();
            services.AddScoped<IHtmlLaborExporterService, HtmlLaborExporterService>();
            services.AddScoped<ICampaignManagerService, CampaignManagerService>();
            services.AddScoped<IWorkOrderQueryService, WorkOrderQueryService>();
            services.AddScoped<ILotQueryService, LotQueryService>();
            services.AddScoped<IDashboardQueryService, DashboardQueryService>();
            services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
            services.AddScoped<GestorOT.Application.Services.IRotationService, RotationService>();
            services.AddScoped<IErpSyncService, ErpSyncService>();
            services.AddHttpClient();
#pragma warning disable EXTEXP0018
            services.AddHybridCache();
#pragma warning restore EXTEXP0018
        }
        else
        {
            Console.WriteLine("WARNING: No database connection string configured. Database features disabled.");
        }
    }
}
