using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Interfaces;

public interface ILotExcelImportService
{
    Task<LotImportSummaryDto> PreviewAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default);
    Task<LotImportResultDto> ExecuteAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default);
    Task<(byte[] Bytes, string FileName)> GenerateTemplateAsync(CancellationToken ct = default);
}
