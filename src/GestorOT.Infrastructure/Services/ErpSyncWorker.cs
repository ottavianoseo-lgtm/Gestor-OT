using Microsoft.EntityFrameworkCore;
using GestorOT.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public class ErpSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ErpSyncWorker> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

    public ErpSyncWorker(IServiceProvider serviceProvider, ILogger<ErpSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ERP Sync Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting global ERP synchronization...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var syncService = scope.ServiceProvider.GetRequiredService<IErpSyncService>();
                    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    // El worker debe iterar sobre todos los tenants que tengan configuracion de ERP
                    var tenants = await context.Tenants
                        .Where(t => !string.IsNullOrEmpty(t.GestorMaxApiKeyEncrypted))
                        .ToListAsync(stoppingToken);

                    foreach (var tenant in tenants)
                    {
                        try
                        {
                            // Verificamos si el tenant ya tiene datos para evitar sync innecesaria
                            var hasData = await context.LaborTypes.AnyAsync(lt => lt.TenantId == tenant.Id, stoppingToken) ||
                                          await context.ErpPeople.AnyAsync(ep => ep.TenantId == tenant.Id, stoppingToken);

                            if (hasData)
                            {
                                _logger.LogInformation("Tenant {TenantId} already has data, skipping initial sync or performing light sync.", tenant.Id);
                                // Opcional: Aquí podrías llamar a una sync ligera o simplemente continuar
                                continue; 
                            }

                            _logger.LogInformation("Syncing data for tenant {TenantId} ({TenantName})...", tenant.Id, tenant.Name);
                            
                            await syncService.SyncLaborTypesAsync(tenant.Id, stoppingToken);
                            await syncService.SyncContactsAsync(tenant.Id, stoppingToken);
                            await syncService.SyncStockAsync(tenant.Id, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error syncing tenant {TenantId}.", tenant.Id);
                        }
                    }
                }

                _logger.LogInformation("Global ERP synchronization completed. Sleeping for {Interval}.", _syncInterval);
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ErpSyncWorker execution loop.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
