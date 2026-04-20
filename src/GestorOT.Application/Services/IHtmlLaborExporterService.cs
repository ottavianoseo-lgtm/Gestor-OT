using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IHtmlLaborExporterService
{
    Task<string> GenerateInteractiveHtmlAsync(Guid workOrderId, string baseUrl, CancellationToken ct = default);
}
