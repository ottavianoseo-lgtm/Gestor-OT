using System.Text.Json;
using GestorOT.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestorOT.Services;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly AsyncLocal<bool> _isAuditing = new();

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_isAuditing.Value || eventData.Context is not ApplicationDbContext dbContext)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var trackedTypes = new HashSet<Type> { typeof(WorkOrder), typeof(Labor) };
        var userEmail = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Email"].FirstOrDefault();

        foreach (var entry in dbContext.ChangeTracker.Entries()
            .Where(e => trackedTypes.Contains(e.Entity.GetType()) &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList())
        {
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = (entry.Entity as ITenantEntity)?.TenantId ?? Guid.Empty,
                UserEmail = userEmail,
                Action = entry.State.ToString(),
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetEntityId(entry.Entity),
                Timestamp = DateTime.UtcNow
            };

            if (entry.State == EntityState.Modified)
            {
                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    oldValues[prop.Metadata.Name] = prop.OriginalValue;
                    newValues[prop.Metadata.Name] = prop.CurrentValue;
                }
                audit.OldValue = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null;
                audit.NewValue = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null;
            }
            else if (entry.State == EntityState.Added)
            {
                var vals = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                    vals[prop.Metadata.Name] = prop.CurrentValue;
                audit.NewValue = JsonSerializer.Serialize(vals);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var vals = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                    vals[prop.Metadata.Name] = prop.OriginalValue;
                audit.OldValue = JsonSerializer.Serialize(vals);
            }

            dbContext.AuditLogs.Add(audit);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (_isAuditing.Value || eventData.Context is not ApplicationDbContext dbContext)
            return base.SavingChanges(eventData, result);

        var trackedTypes = new HashSet<Type> { typeof(WorkOrder), typeof(Labor) };
        var userEmail = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Email"].FirstOrDefault();

        foreach (var entry in dbContext.ChangeTracker.Entries()
            .Where(e => trackedTypes.Contains(e.Entity.GetType()) &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList())
        {
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = (entry.Entity as ITenantEntity)?.TenantId ?? Guid.Empty,
                UserEmail = userEmail,
                Action = entry.State.ToString(),
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetEntityId(entry.Entity),
                Timestamp = DateTime.UtcNow
            };

            if (entry.State == EntityState.Modified)
            {
                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    oldValues[prop.Metadata.Name] = prop.OriginalValue;
                    newValues[prop.Metadata.Name] = prop.CurrentValue;
                }
                audit.OldValue = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null;
                audit.NewValue = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null;
            }
            else if (entry.State == EntityState.Added)
            {
                var vals = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                    vals[prop.Metadata.Name] = prop.CurrentValue;
                audit.NewValue = JsonSerializer.Serialize(vals);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var vals = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                    vals[prop.Metadata.Name] = prop.OriginalValue;
                audit.OldValue = JsonSerializer.Serialize(vals);
            }

            dbContext.AuditLogs.Add(audit);
        }

        return base.SavingChanges(eventData, result);
    }

    private static string? GetEntityId(object entity) => entity switch
    {
        WorkOrder wo => wo.Id.ToString(),
        Labor l => l.Id.ToString(),
        _ => null
    };
}
