using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IWorkOrderPdfExporterService
{
    Task<byte[]> GeneratePdfAsync(Guid workOrderId, CancellationToken ct = default);
}
