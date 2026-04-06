namespace GestorOT.Application.Services;

public interface IIsoXmlExporterService
{
    Task<byte[]> ExportWorkOrderAsIsoXmlAsync(Guid workOrderId, CancellationToken ct = default);
}
