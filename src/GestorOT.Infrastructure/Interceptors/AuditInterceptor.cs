using System.Text.Json;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Infrastructure.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly AsyncLocal<bool> _isAuditing = new();

    private static readonly HashSet<Type> TrackedTypes = [typeof(WorkOrder), typeof(Labor), typeof(CampaignLot)];

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        GenerateAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        GenerateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void GenerateAuditLogs(DbContext? context)
    {
        if (_isAuditing.Value || context is not ApplicationDbContext dbContext)
            return;

        var userEmail = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Email"].FirstOrDefault();

        foreach (var entry in dbContext.ChangeTracker.Entries()
            .Where(e => TrackedTypes.Contains(e.Entity.GetType()) &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
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

            switch (entry.State)
            {
                case EntityState.Modified:
                    audit.OldValue = SerializeProperties(entry.Properties.Where(p => p.IsModified), p => p.OriginalValue);
                    audit.NewValue = SerializeProperties(entry.Properties.Where(p => p.IsModified), p => p.CurrentValue);
                    break;
                case EntityState.Added:
                    audit.NewValue = SerializeProperties(entry.Properties, p => p.CurrentValue);
                    break;
                case EntityState.Deleted:
                    audit.OldValue = SerializeProperties(entry.Properties, p => p.OriginalValue);
                    break;
            }

            dbContext.AuditLogs.Add(audit);
        }
    }

    private static string? SerializeProperties(IEnumerable<PropertyEntry> properties, Func<PropertyEntry, object?> valueSelector)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in properties)
            dict[prop.Metadata.Name] = ToJsonSafeValue(valueSelector(prop));
        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }

    /// <summary>
    /// System.Text.Json escribe los valores por reflexion, asi que no puede volcar objetos
    /// arbitrarios. El caso que rompia: al vincular una geometria a un lote que no tenia, se
    /// modifica CampaignLot.Geometry (NetTopologySuite) y sus Coordinate exponen Z/M en NaN.
    /// Con el manejo de numeros estricto (default) Serialize tira ArgumentException, que el
    /// GlobalExceptionMiddleware mapea a un 400 "Argumento invalido": el guardado parecia
    /// fallar por un dato del usuario y en realidad era la auditoria. Las geometrias van como
    /// WKT (que ademas es lo unico legible) y los decimales no finitos como null.
    /// </summary>
    private static object? ToJsonSafeValue(object? value) => value switch
    {
        null => null,
        Geometry geometry => new WKTWriter().Write(geometry),
        double d when double.IsNaN(d) || double.IsInfinity(d) => null,
        float f when float.IsNaN(f) || float.IsInfinity(f) => null,
        _ when IsSimpleType(value.GetType()) => value,
        _ => value.ToString()
    };

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }

    private static string? GetEntityId(object entity) => entity switch
    {
        WorkOrder wo => wo.Id.ToString(),
        Labor l => l.Id.ToString(),
        CampaignLot cl => cl.Id.ToString(),
        _ => null
    };
}
