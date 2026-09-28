using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class ErpSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ErpSyncWorker> _logger;

    public ErpSyncWorker(IServiceProvider serviceProvider, ILogger<ErpSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ERP Sync Worker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Esperar a que pase el tiempo configurado (ej. una vez al día a las 2 AM)
                // Para pruebas, podrías bajarlo a 1 hora o usar un cron
                await SynchronizeAllTenants(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en el ERP Sync Worker.");
            }

            // Esperar 24 horas antes de la próxima ejecución
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SynchronizeAllTenants(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando sincronización masiva de todos los tenants...");

        List<Tenant> tenants;
        using (var listScope = _serviceProvider.CreateScope())
        {
            var context = listScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            tenants = await context.Tenants
                .AsNoTracking()
                .Where(t => !string.IsNullOrEmpty(t.GestorMaxApiKeyEncrypted) && !string.IsNullOrEmpty(t.GestorMaxDatabaseId))
                .ToListAsync(ct);
        }

        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation($"Sincronizando Tenant: {tenant.Name} ({tenant.Id})");

                // Un scope (y un DbContext) por empresa: con uno solo para todas, el
                // change tracker juntaba los catálogos de todas las empresas y cada
                // SaveChanges recorría miles de entidades de más.
                using var scope = _serviceProvider.CreateScope();
                var erpSyncService = scope.ServiceProvider.GetRequiredService<IErpSyncService>();
                await erpSyncService.TotalSyncAsync(tenant.Id, ct);

                _logger.LogInformation($"Sincronización exitosa para {tenant.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Falló la sincronización para el tenant {tenant.Id}");
            }
        }

        _logger.LogInformation("Sincronización masiva finalizada.");
    }
}
