using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestorOT.Infrastructure.Interceptors;

public class CampaignLockedInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateCampaignLock(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext dbContext)
        {
            await ValidateCampaignLockAsync(dbContext, cancellationToken);
            await ValidateWorkOrderLockAsync(dbContext, cancellationToken);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ValidateCampaignLock(DbContext? context)
    {
        if (context is not ApplicationDbContext dbContext) return;

        var campaignIds = CollectCampaignIds(dbContext);
        if (campaignIds.Count == 0) return;

        var lockedCampaigns = dbContext.Campaigns
            .IgnoreQueryFilters()
            .Where(c => campaignIds.Contains(c.Id) && c.Status == "Locked")
            .Select(c => c.Name)
            .ToList();

        if (lockedCampaigns.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se pueden realizar cambios en campañas cerradas: {string.Join(", ", lockedCampaigns)}");
        }

        // OT Lock check (sync)
        ValidateWorkOrderLock(dbContext);
    }

    private void ValidateWorkOrderLock(ApplicationDbContext dbContext)
    {
        var workOrderIds = CollectWorkOrderIds(dbContext);
        if (workOrderIds.Count == 0) return;

        var lockedOrders = dbContext.WorkOrders
            .IgnoreQueryFilters()
            .Include(w => w.WorkOrderStatus)
            .Where(w => workOrderIds.Contains(w.Id) && w.WorkOrderStatus != null && !w.WorkOrderStatus.IsEditable)
            .Select(w => w.Description)
            .ToList();

        if (lockedOrders.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se pueden modificar labores en órdenes de trabajo bloqueadas: {string.Join(", ", lockedOrders)}");
        }
    }

    private async Task ValidateWorkOrderLockAsync(ApplicationDbContext dbContext, CancellationToken ct)
    {
        var workOrderIds = CollectWorkOrderIds(dbContext);
        if (workOrderIds.Count == 0) return;

        var lockedOrders = await dbContext.WorkOrders
            .IgnoreQueryFilters()
            .Include(w => w.WorkOrderStatus)
            .Where(w => workOrderIds.Contains(w.Id) && w.WorkOrderStatus != null && !w.WorkOrderStatus.IsEditable)
            .Select(w => w.Description)
            .ToListAsync(ct);

        if (lockedOrders.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se pueden modificar labores en órdenes de trabajo bloqueadas: {string.Join(", ", lockedOrders)}");
        }
    }

    private static HashSet<Guid> CollectWorkOrderIds(ApplicationDbContext dbContext)
    {
        var ids = new HashSet<Guid>();

        foreach (var entry in dbContext.ChangeTracker.Entries<WorkOrder>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            ids.Add(entry.Entity.Id);
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<Labor>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity.WorkOrderId.HasValue)
                ids.Add(entry.Entity.WorkOrderId.Value);
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<LaborSupply>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity.Labor?.WorkOrderId != null)
                ids.Add(entry.Entity.Labor.WorkOrderId.Value);
            // If labor not loaded, we might miss it, but usually it's loaded in OT context
        }

        return ids;
    }

    private async Task ValidateCampaignLockAsync(ApplicationDbContext dbContext, CancellationToken ct)
    {
        var campaignIds = CollectCampaignIds(dbContext);
        if (campaignIds.Count == 0) return;

        var lockedCampaigns = await dbContext.Campaigns
            .IgnoreQueryFilters()
            .Where(c => campaignIds.Contains(c.Id) && c.Status == "Locked")
            .Select(c => c.Name)
            .ToListAsync(ct);

        if (lockedCampaigns.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se pueden modificar órdenes de trabajo en campañas cerradas: {string.Join(", ", lockedCampaigns)}");
        }
    }

    private static HashSet<Guid> CollectCampaignIds(ApplicationDbContext dbContext)
    {
        var campaignIds = new HashSet<Guid>();

        // Check WorkOrders
        foreach (var entry in dbContext.ChangeTracker.Entries<WorkOrder>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity.CampaignId.HasValue)
                campaignIds.Add(entry.Entity.CampaignId.Value);
        }

        // Check Labors
        foreach (var entry in dbContext.ChangeTracker.Entries<Labor>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity.WorkOrderId.HasValue)
            {
                // Try to get from tracker
                var woEntry = dbContext.ChangeTracker.Entries<WorkOrder>()
                    .FirstOrDefault(w => w.Entity.Id == entry.Entity.WorkOrderId.Value);
                
                if (woEntry?.Entity.CampaignId.HasValue == true)
                {
                    campaignIds.Add(woEntry.Entity.CampaignId.Value);
                }
                else
                {
                    // Fallback to database lookup if not tracked (slow but necessary if not loaded)
                    var campaignId = dbContext.WorkOrders
                        .IgnoreQueryFilters()
                        .Where(w => w.Id == entry.Entity.WorkOrderId.Value)
                        .Select(w => w.CampaignId)
                        .FirstOrDefault();
                    
                    if (campaignId.HasValue)
                        campaignIds.Add(campaignId.Value);
                }
            }
        }

        // Check CampaignLots
        foreach (var entry in dbContext.ChangeTracker.Entries<CampaignLot>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            campaignIds.Add(entry.Entity.CampaignId);
        }

        // Check Rotations
        foreach (var entry in dbContext.ChangeTracker.Entries<Rotation>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            // Try to get from tracker
            var clEntry = dbContext.ChangeTracker.Entries<CampaignLot>()
                .FirstOrDefault(cl => cl.Entity.Id == entry.Entity.CampaignLotId);
            
            if (clEntry != null)
            {
                campaignIds.Add(clEntry.Entity.CampaignId);
            }
            else
            {
                var campaignId = dbContext.CampaignLots
                    .IgnoreQueryFilters()
                    .Where(cl => cl.Id == entry.Entity.CampaignLotId)
                    .Select(cl => cl.CampaignId)
                    .FirstOrDefault();
                
                if (campaignId != Guid.Empty)
                    campaignIds.Add(campaignId);
            }
        }

        return campaignIds;
    }
}
