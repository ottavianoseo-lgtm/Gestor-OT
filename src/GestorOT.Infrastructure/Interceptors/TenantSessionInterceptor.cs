using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestorOT.Infrastructure.Interceptors;

public class TenantSessionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantSessionInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid CurrentTenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var tenantHeader = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                if (Guid.TryParse(tenantHeader, out var tenantId))
                    return tenantId;
            }
            return Guid.Empty;
        }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId;

        if (tenantId != Guid.Empty)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "RESET app.current_tenant";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        var tenantId = CurrentTenantId;

        if (tenantId != Guid.Empty)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
            cmd.ExecuteNonQuery();
        }
        else
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "RESET app.current_tenant";
            cmd.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }
}
