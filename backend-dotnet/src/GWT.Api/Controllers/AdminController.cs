using GWT.Application.DTOs.Common;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using GWT.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly INavSyncService _navSync;
    private readonly GwtDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;

    public AdminController(INavSyncService navSync, GwtDbContext db, IConfiguration config, ILogger<AdminController> logger)
    {
        _navSync = navSync;
        _db      = db;
        _config  = config;
        _logger  = logger;
    }

    /// <summary>Returns fund_meta row counts by region and NAV freshness. No auth required.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var indiaTotal       = await _db.FundMetas.CountAsync(f => f.Region == Region.INDIA, ct);
        var indiaWithNav     = await _db.FundMetas.CountAsync(f => f.Region == Region.INDIA && f.LatestNav != null, ct);
        var indiaTodayNav    = await _db.FundMetas.CountAsync(f => f.Region == Region.INDIA && f.NavDate != null && f.NavDate.Value.Date == today, ct);

        var globalTotal      = await _db.FundMetas.CountAsync(f => f.Region == Region.GLOBAL, ct);
        var globalWithNav    = await _db.FundMetas.CountAsync(f => f.Region == Region.GLOBAL && f.LatestNav != null, ct);
        var globalTodayNav   = await _db.FundMetas.CountAsync(f => f.Region == Region.GLOBAL && f.NavDate != null && f.NavDate.Value.Date == today, ct);

        var heldFunds        = await _db.FundMetas.CountAsync(f => f.Holdings.Any(), ct);
        var totalHoldings    = await _db.Holdings.CountAsync(ct);
        var totalUsers       = await _db.Users.CountAsync(ct);

        return Ok(new
        {
            asOf = DateTime.UtcNow,
            india = new
            {
                total       = indiaTotal,
                withNav     = indiaWithNav,
                todayNavCount = indiaTodayNav,
                seeded      = indiaTotal > 500,
            },
            global = new
            {
                total       = globalTotal,
                withNav     = globalWithNav,
                todayNavCount = globalTodayNav,
                seeded      = globalTotal > 500,
            },
            portfolio = new
            {
                heldFunds,
                totalHoldings,
                totalUsers,
            }
        });
    }

    /// <summary>Manually trigger a NAV sync for all held funds. Requires X-Admin-Key header.</summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TriggerSync(CancellationToken ct)
    {
        var expectedKey = _config["AdminKey"];
        var providedKey = Request.Headers["X-Admin-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
        {
            _logger.LogWarning("Rejected admin sync attempt from {IP}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(ApiResponse.Fail("Invalid or missing admin key."));
        }

        await _navSync.SyncAllAsync(ct);
        return Ok(ApiResponse.Ok());
    }
}
