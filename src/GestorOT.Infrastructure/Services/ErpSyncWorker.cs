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
        _logger.LogInformation("Intermediary Data Fetch Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting data fetch from GestorMaxIntegrator...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var syncService = scope.ServiceProvider.GetRequiredService<IErpSyncService>();
                    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    // Obtenemos el tenant por defecto o el principal
                    // En el futuro, esto podría ser más dinámico
                    var tenants = await context.Tenants.ToListAsync(stoppingToken);

                    foreach (var tenant in tenants)
                    {
                        try
                        {
                            _logger.LogInformation("Updating local cache for tenant {TenantId} from Intermediary...", tenant.Id);
                            
                            await syncService.SyncLaborTypesAsync(tenant.Id, stoppingToken);
                            await syncService.SyncContactsAsync(tenant.Id, stoppingToken);
                            await syncService.SyncStockAsync(tenant.Id, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error fetching data for tenant {TenantId} from Intermediary.", tenant.Id);
                        }
                    }
                }

                _logger.LogInformation("Data fetch from Intermediary completed. Sleeping for {Interval}.", _syncInterval);
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Intermediary Data Fetch execution loop.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
