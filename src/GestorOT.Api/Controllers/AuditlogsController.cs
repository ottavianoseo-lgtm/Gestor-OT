using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/auditlogs")]
public class AuditlogsController : ControllerBase
{
    private readonly IAuditLogQueryService _queryService;

    public AuditlogsController(IAuditLogQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var logs = await _queryService.GetLogsAsync(page, pageSize);
        return Ok(logs);
    }
}
