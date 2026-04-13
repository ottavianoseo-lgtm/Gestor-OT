namespace GestorOT.Application.Services;

public interface ICampaignManagerService
{
    Task<int> ImportLotsFromPreviousCampaignAsync(Guid newCampaignId, Guid previousCampaignId, bool useSuperficieFromPrevious = false, CancellationToken ct = default);
}
