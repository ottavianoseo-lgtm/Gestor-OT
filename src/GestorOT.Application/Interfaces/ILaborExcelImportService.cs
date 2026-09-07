using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Interfaces;

public interface ILaborExcelImportService
{
    Task<LaborImportPreviewDto> PreviewAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default);
    Task<LaborImportResultDto> ExecuteAsync(Guid campaignId, Stream fileStream, List<LaborImportSupplyMappingDto> mappings, CancellationToken ct = default);
    Task<(byte[] Bytes, string FileName)> GenerateTemplateAsync(CancellationToken ct = default);
}
