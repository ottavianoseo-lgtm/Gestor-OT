using GestorOT.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestorOT.Services;

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
                $"No se pueden modificar órdenes de trabajo en campañas cerradas: {string.Join(", ", lockedCampaigns)}");
        }
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

        foreach (var entry in dbContext.ChangeTracker.Entries<WorkOrder>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity.CampaignId.HasValue)
                campaignIds.Add(entry.Entity.CampaignId.Value);
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<Labor>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity.WorkOrderId.HasValue)
            {
                var wo = dbContext.ChangeTracker.Entries<WorkOrder>()
                    .FirstOrDefault(w => w.Entity.Id == entry.Entity.WorkOrderId.Value);
                if (wo?.Entity.CampaignId.HasValue == true)
                    campaignIds.Add(wo.Entity.CampaignId.Value);
            }
        }

        return campaignIds;
    }
}
