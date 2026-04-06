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
                var tenantHeader = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                if (Guid.TryParse(tenantHeader, out var tenantId))
                    return tenantId;
            }
            return Guid.Empty;
        }
    }
}
