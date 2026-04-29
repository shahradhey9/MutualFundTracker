using System.Security.Claims;
using FluentValidation;
using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GWT.Api.Controllers;

[ApiController]
[Route("api/funds")]
public class FundsController : ControllerBase
{
    private readonly IFundService _funds;
    private readonly IAmfiService _amfi;
    private readonly IYahooFinanceService _yahoo;
    private readonly IFundMetaRepository _fundMeta;
    private readonly IPortfolioService _portfolio;
    private readonly IValidator<EnsureFundRequestDto> _ensureValidator;

    public FundsController(
        IFundService funds,
        IAmfiService amfi,
        IYahooFinanceService yahoo,
        IFundMetaRepository fundMeta,
        IPortfolioService portfolio,
        IValidator<EnsureFundRequestDto> ensureValidator)
    {
        _funds = funds;
        _amfi = amfi;
        _yahoo = yahoo;
        _fundMeta = fundMeta;
        _portfolio = portfolio;
        _ensureValidator = ensureValidator;
    }

    /// <summary>Full-text search for funds. region=INDIA returns AMFI mutual funds; region=GLOBAL returns Yahoo Finance results.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] Region region = Region.INDIA,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var results = await _funds.SearchAsync(q, region, ct);
        // Wrap as { results: [...] } to match the frontend contract
        return Ok(new { results });
    }

    /// <summary>Get the latest NAV for a single fund by ticker.</summary>
    [HttpGet("nav/{ticker}")]
    public async Task<IActionResult> GetNav(
        string ticker,
        [FromQuery] Region region = Region.INDIA,
        CancellationToken ct = default)
    {
        var nav = await _funds.GetNavAsync(ticker, region, ct);
        return Ok(nav);
    }

    /// <summary>
    /// Force-refresh the in-memory NAV caches for both India (AMFI) and Global (Yahoo Finance)
    /// immediately, bypassing the normal 30-minute TTL. Also evicts this user's portfolio cache
    /// so the next portfolio fetch returns prices from the freshly-populated NAV caches.
    /// Requires a valid JWT — no admin key needed.
    /// </summary>
    [HttpPost("refresh-nav")]
    [Authorize]
    public async Task<IActionResult> RefreshNav(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        // India: force-refresh AMFI in-memory cache and persist all NAVs to fund_meta.
        var amfiTask = Task.Run(async () =>
        {
            var allNavs = await _amfi.ForceRefreshAsync(ct);
            if (allNavs.Count > 0)
            {
                var updates = allNavs.Select(f => ($"IN-{f.SchemeCode}", f.Nav, DateTime.SpecifyKind(f.NavDate, DateTimeKind.Utc)));
                await _fundMeta.UpdateNavBatchAsync(updates, ct);
            }
        }, ct);

        // Global: force-refresh Yahoo in-memory cache and persist NAVs to fund_meta.
        var yahooTask = Task.Run(async () =>
        {
            var globalFunds = await _fundMeta.GetAllByRegionAsync(Region.GLOBAL, ct);
            var tickers     = globalFunds.Select(f => f.Ticker).Distinct().ToList();
            if (tickers.Count > 0)
            {
                var quotes = await _yahoo.ForceRefreshGlobalNavsAsync(tickers, ct);
                if (quotes.Count > 0)
                {
                    var tickerToId = globalFunds
                        .Where(f => !string.IsNullOrEmpty(f.Ticker))
                        .ToDictionary(f => f.Ticker, f => f.Id, StringComparer.OrdinalIgnoreCase);
                    var updates = quotes
                        .Where(kv => tickerToId.ContainsKey(kv.Key))
                        .Select(kv => (tickerToId[kv.Key], kv.Value.Price, kv.Value.Timestamp));
                    await _fundMeta.UpdateNavBatchAsync(updates, ct);
                }
            }
        }, ct);

        await Task.WhenAll(amfiTask, yahooTask);

        // Evict ALL cached portfolio responses so every user sees updated NAVs on
        // their next portfolio load — not just the user who clicked Refresh.
        _portfolio.InvalidateAllCaches();

        return Ok(new { success = true, refreshedAt = DateTime.UtcNow });
    }

    /// <summary>Upsert a fund into the catalogue (required before adding a holding).</summary>
    [HttpPost("ensure")]
    [Authorize]
    public async Task<IActionResult> EnsureFund([FromBody] EnsureFundRequestDto request, CancellationToken ct)
    {
        var validation = await _ensureValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { error = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)) });

        var result = await _funds.EnsureFundAsync(request, ct);
        return Ok(result);
    }
}
