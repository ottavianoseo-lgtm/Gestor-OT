using GestorOT.Application.Interfaces;

namespace GestorOT.Api.Extensions;

public class CampaignContextService : ICampaignContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CampaignContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CurrentCampaignId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var campaignHeader = httpContext.Request.Headers["X-Campaign-ID"].FirstOrDefault();
                if (Guid.TryParse(campaignHeader, out var campaignId))
                    return campaignId;
            }
            return null;
        }
    }
}
