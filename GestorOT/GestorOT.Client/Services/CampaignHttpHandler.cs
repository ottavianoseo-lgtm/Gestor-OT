namespace GestorOT.Client.Services;

public class CampaignHttpHandler : DelegatingHandler
{
    private readonly CampaignState _campaignState;

    public CampaignHttpHandler(CampaignState campaignState)
    {
        _campaignState = campaignState;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_campaignState.CurrentCampaign != null)
        {
            request.Headers.Remove("X-Campaign-ID");
            request.Headers.Add("X-Campaign-ID", _campaignState.CurrentCampaign.Id.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
