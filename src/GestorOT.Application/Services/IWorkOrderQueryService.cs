using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IWorkOrderQueryService
{
    Task<List<WorkOrderDto>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<WorkOrderDto>> GetPagedAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<WorkOrderDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
