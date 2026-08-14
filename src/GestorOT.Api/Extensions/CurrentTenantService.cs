using System.Security.Claims;
using GestorOT.Application.Interfaces;

namespace GestorOT.Api.Extensions;

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var user = httpContext.User;
                var isSuperAdmin = user?.IsInRole("SuperAdmin") == true 
                                   || user?.FindFirst(ClaimTypes.Role)?.Value == "SuperAdmin";

                if (isSuperAdmin)
                {
                    var tenantHeader = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                    if (Guid.TryParse(tenantHeader, out var headerTenantId))
                        return headerTenantId;

                    return Guid.Empty;
                }

                // First try from authenticated user claims
                var claimTenant = user?.FindFirst("tenant_id")?.Value;
                if (Guid.TryParse(claimTenant, out var userTenantId) && userTenantId != Guid.Empty)
                {
                    return userTenantId;
                }

                // Fallback to X-Tenant-ID header for legacy or initialization requests
                var fallbackHeader = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                if (Guid.TryParse(fallbackHeader, out var tenantId))
                    return tenantId;
            }
            return Guid.Empty;
        }
    }
}
