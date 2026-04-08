using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IAuditLogQueryService
{
    Task<List<AuditLogDto>> GetLogsAsync(int page, int pageSize, CancellationToken ct = default);
}
