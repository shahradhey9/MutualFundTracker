using GWT.Application.DTOs.Common;
using GWT.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly INavSyncService _navSync;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;

    public AdminController(INavSyncService navSync, IConfiguration config, ILogger<AdminController> logger)
    {
        _navSync = navSync;
        _config = config;
        _logger = logger;
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
