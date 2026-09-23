using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Interfaces;

public interface ILaborExcelImportService
{
    Task<LaborImportPreviewDto> PreviewAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default);
    Task<LaborImportResultDto> ExecuteAsync(Guid campaignId, Stream fileStream, List<LaborImportSupplyMappingDto> mappings, List<LaborImportTypeMappingDto>? laborTypeMappings = null, List<LaborImportSupplierMappingDto>? supplierMappings = null, CancellationToken ct = default);
    Task<(byte[] Bytes, string FileName)> GenerateTemplateAsync(CancellationToken ct = default);
    /// <summary>
    /// Sube un Excel sin mostrar preview: importa de una las filas en verde (full
    /// match) y persiste el resto como lote pendiente para matcheo manual.
    /// </summary>
    Task<LaborImportUploadResultDto> UploadAsync(Guid campaignId, Stream fileStream, string fileName, string? uploadedBy, bool force = false, CancellationToken ct = default);
    Task<List<LaborImportBatchDto>> GetBatchesAsync(Guid campaignId, CancellationToken ct = default);
    Task<LaborImportBatchDetailDto?> GetBatchDetailAsync(Guid batchId, CancellationToken ct = default);
    Task SaveBatchMappingsAsync(Guid batchId, LaborImportBatchMappingsDto mappings, CancellationToken ct = default);
    /// <summary>Importa filas pendientes ya matchables con los mappings del batch.</summary>
    Task<LaborImportBatchResolveResultDto> ImportBatchRowsAsync(Guid batchId, List<int>? rowIndexes, CancellationToken ct = default);
    /// <summary>
    /// Guarda las correcciones hechas sobre una fila pendiente sin importarla: la
    /// revisión y la importación son dos pasos separados a propósito.
    /// </summary>
    Task<LaborImportBatchDetailDto?> UpdatePendingRowAsync(Guid batchId, int rowIndex, LaborImportRowEditDto edit, CancellationToken ct = default);
    Task<LaborImportBatchResolveResultDto> DiscardBatchRowsAsync(Guid batchId, List<int>? rowIndexes, CancellationToken ct = default);
    /// <summary>Re-evalúa matches automáticos contra el estado actual de los catálogos.</summary>
    Task<LaborImportBatchDetailDto?> ReevaluateBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(Guid campaignId, CancellationToken ct = default);
}
