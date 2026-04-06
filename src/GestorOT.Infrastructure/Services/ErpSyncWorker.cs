using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using GestorOT.Application.Interfaces;

namespace GestorOT.Infrastructure.Services;

public class ErpSyncWorker : BackgroundService
{
    private readonly ILogger<ErpSyncWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

    public ErpSyncWorker(ILogger<ErpSyncWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ERP Sync Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IErpSyncService>();

                _logger.LogInformation("Starting ERP synchronization...");
                
                await syncService.SyncLaborTypesAsync(stoppingToken);
                await syncService.SyncEmployeesAsync(stoppingToken);
                await syncService.SyncStockAsync(stoppingToken);

                _logger.LogInformation("ERP synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during ERP synchronization.");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("ERP Sync Worker is stopping.");
    }
}
