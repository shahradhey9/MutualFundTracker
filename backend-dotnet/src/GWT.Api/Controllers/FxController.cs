using GWT.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/fx")]
public class FxController : ControllerBase
{
    private readonly IFxService _fx;

    public FxController(IFxService fx) => _fx = fx;

    /// <summary>
    /// Returns the exchange rate between two currencies.
    /// Proxies Frankfurter server-side to avoid browser CORS issues.
    /// No authentication required — FX rates are public data.
    /// </summary>
    [HttpGet("rate")]
    public async Task<IActionResult> GetRate(
        [FromQuery] string from = "USD",
        [FromQuery] string to = "INR",
        CancellationToken ct = default)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();

        if (from.Length != 3 || to.Length != 3)
            return BadRequest(new { error = "Currency codes must be 3 characters (e.g. USD, INR)." });

        var rate = await _fx.GetRateAsync(from, to, ct);
        return Ok(rate);
    }
}
