using GestorOT.Shared.Dtos;
using Microsoft.JSInterop;

namespace GestorOT.Client.Services;

public class CampaignState
{
    private readonly IJSRuntime _jsRuntime;
    public CampaignSummaryDto? CurrentCampaign { get; private set; }
    public bool IsSelected => CurrentCampaign != null;

    public event Action? OnChange;

    public CampaignState(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public void SetCampaign(CampaignSummaryDto campaign)
    {
        CurrentCampaign = campaign;
        _jsRuntime.InvokeVoidAsync("localStorage.setItem", "selected_campaign_id", campaign.Id.ToString());
        OnChange?.Invoke();
    }

    public void Clear()
    {
        CurrentCampaign = null;
        _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "selected_campaign_id");
        OnChange?.Invoke();
    }
}
