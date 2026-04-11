using GWT.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GWT.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly GwtDbContext _db;

    public HealthController(GwtDbContext db) => _db = db;

    /// <summary>
    /// Instant liveness check — no DB query, so it responds immediately even on cold start.
    /// Used by the frontend keep-alive ping and Render's health-check probe.
    /// Accessible at both /health (Render probe) and /api/health (frontend axios base URL).
    /// </summary>
    [HttpGet("/health")]
    [HttpGet("/api/health")]
    public IActionResult Health() =>
        Ok(new { status = "ok", ts = DateTime.UtcNow });

    /// <summary>Deep health check — verifies DB connectivity. Slower, use sparingly.</summary>
    [HttpGet("/api/health/deep")]
    public async Task<IActionResult> DeepHealth(CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Ok(new { status = "ok", db = "connected", ts = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "degraded", db = "error", error = ex.Message });
        }
    }
}
