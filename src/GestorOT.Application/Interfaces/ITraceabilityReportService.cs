using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Interfaces;

public interface ITraceabilityReportService
{
    Task<CampaignTraceabilityReportDto> GetCampaignTraceabilityAsync(Guid campaignId, CancellationToken ct = default);
    Task<LotTraceabilityReportDto?> GetLotTraceabilityAsync(Guid campaignId, Guid lotId, CancellationToken ct = default);
    Task<FieldTraceabilityReportDto?> GetFieldTraceabilityAsync(Guid campaignId, Guid fieldId, CancellationToken ct = default);
}
