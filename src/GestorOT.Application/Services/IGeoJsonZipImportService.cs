using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IGeoJsonZipImportService
{
    /// <summary>
    /// Lee un archivo .zip que contiene archivos .geojson (o GeoJSONs sueltos), extrae la geometría
    /// y el lote_id de cada uno, y busca en la base de datos el lote correspondiente (por Id o ExternalErpId).
    /// Devuelve la propuesta de vinculación con la comparación de superficie declarada vs GIS.
    /// </summary>
    Task<GeoJsonZipPreviewResultDto> PreviewZipAsync(Stream zipStream, CancellationToken ct = default);

    /// <summary>
    /// Aplica las geometrías a los lotes correspondientes en una sola transacción atómica.
    /// </summary>
    Task<GeoJsonZipApplyResultDto> ApplyAsync(GeoJsonZipApplyRequestDto request, CancellationToken ct = default);
}
