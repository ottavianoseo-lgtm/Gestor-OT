using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class AuditLogQueryService : IAuditLogQueryService
{
    private readonly IApplicationDbContext _context;

    public AuditLogQueryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AuditLogDto>> GetLogsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogDto(
                x.Id,
                x.UserEmail,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.OldValue,
                x.NewValue,
                x.Timestamp
            ))
            .ToListAsync(ct);
    }
}
