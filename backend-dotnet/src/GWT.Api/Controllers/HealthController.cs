using GWT.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GWT.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly GwtDbContext _db;

    public HealthController(GwtDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Ok(new { status = "ok", db = "connected", ts = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "degraded", db = "error", error = ex.Message, ts = DateTime.UtcNow });
        }
    }
}
