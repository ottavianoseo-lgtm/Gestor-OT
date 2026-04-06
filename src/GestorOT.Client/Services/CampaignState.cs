using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

public class CampaignState
{
    public CampaignSummaryDto? CurrentCampaign { get; private set; }
    public bool IsSelected => CurrentCampaign != null;

    public event Action? OnChange;

    public void SetCampaign(CampaignSummaryDto campaign)
    {
        CurrentCampaign = campaign;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        CurrentCampaign = null;
        OnChange?.Invoke();
    }
}
