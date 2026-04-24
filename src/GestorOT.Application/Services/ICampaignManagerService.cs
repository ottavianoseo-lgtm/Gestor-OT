using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface ICampaignManagerService
{
    Task<int> ImportLotsFromPreviousCampaignAsync(Guid newCampaignId, Guid previousCampaignId, bool useSuperficieFromPrevious = false, CancellationToken ct = default);

    /// <summary>
    /// Assigns all lots belonging to <paramref name="fieldId"/> to campaign <paramref name="campaignId"/>.
    /// Idempotent: already-assigned lots are skipped.
    /// Returns a summary with the number of lots assigned and skipped.
    /// </summary>
    Task<BatchAssignLotsResult> BatchAssignLotsByFieldAsync(Guid campaignId, Guid fieldId, CancellationToken ct = default);
}
