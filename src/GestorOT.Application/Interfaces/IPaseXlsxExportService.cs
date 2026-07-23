namespace GestorOT.Application.Interfaces;

public interface IPaseXlsxExportService
{
    Task<(Stream FileStream, string FileName)> ExportLoteAsync(
        Guid tenantId, Guid loteId, CancellationToken ct = default);
}
