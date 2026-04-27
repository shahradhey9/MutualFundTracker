using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.Jobs;

/// <summary>
/// Hosted background service that runs per-country NAV syncs at midnight in each fund's
/// local exchange timezone.  One loop runs per known timezone; adding a new exchange
/// timezone only requires appending an entry to <see cref="KnownTimezones"/>.
/// </summary>
public class NavSyncBackgroundService : BackgroundService
{
    // ── Known exchange timezones ─────────────────────────────────────────────────
    // IsIndia = true  → SyncIndiaAsync (AMFI)
    // IsIndia = false → SyncGlobalByTimezoneAsync (Yahoo Finance)
    private record TimezoneConfig(string IanaId, bool IsIndia);

    private static readonly TimezoneConfig[] KnownTimezones =
    [
        new("Asia/Kolkata",     IsIndia: true),   // India — AMFI
        new("America/New_York", IsIndia: false),  // US (NYSE / NASDAQ)
        new("Europe/London",    IsIndia: false),  // UK (LSE)
        new("Europe/Paris",     IsIndia: false),  // France (Euronext)
        // Add more exchange timezones here as needed
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IYahooFinanceService _yahoo;
    private readonly ILogger<NavSyncBackgroundService> _logger;

    public NavSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IYahooFinanceService yahoo,
        ILogger<NavSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _yahoo = yahoo;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Warm up external API caches 5s after startup (non-fatal)
        _ = WarmUpAsync(stoppingToken);

        // Start one independent loop per timezone — all run concurrently
        var loops = KnownTimezones.Select(tz => RunTimezoneLoopAsync(tz, stoppingToken));

        // Also run a 4-hour global NAV cache refresh loop (independent of midnight syncs)
        return Task.WhenAll([..loops, RunGlobalNavCacheRefreshLoopAsync(stoppingToken)]);
    }

    // ── Per-timezone sync loop ───────────────────────────────────────────────────

    private async Task RunTimezoneLoopAsync(TimezoneConfig config, CancellationToken ct)
    {
        var tz = ResolveTimeZone(config.IanaId);
        _logger.LogInformation(
            "NAV sync loop started — {Timezone} ({Region}), fires daily at midnight local time.",
            config.IanaId, config.IsIndia ? "India/AMFI" : "Global/Yahoo");

        while (!ct.IsCancellationRequested)
        {
            var delay = DelayUntilMidnight(tz);
            _logger.LogDebug("Next {Timezone} NAV sync in {Delay}", config.IanaId, delay);

            try
            {
                await Task.Delay(delay, ct);
                await RunSyncAsync(config, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NAV sync error for {Timezone} — retrying in 5 minutes", config.IanaId);
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
        }
    }

    private async Task RunSyncAsync(TimezoneConfig config, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<INavSyncService>();

        if (config.IsIndia)
            await svc.SyncIndiaAsync(ct);
        else
            await svc.SyncGlobalByTimezoneAsync(config.IanaId, ct);
    }

    // ── Global NAV cache refresh loop (every 4 hours) ───────────────────────────

    private static readonly TimeSpan GlobalNavCacheInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Runs independently of the midnight loops. Immediately warms the global NAV cache
    /// at startup, then refreshes every 4 hours — same cadence as AMFI's process cache TTL.
    /// </summary>
    private async Task RunGlobalNavCacheRefreshLoopAsync(CancellationToken ct)
    {
        // First run: wait for the startup warm-up (crumb + AMFI) to finish, then immediately
        // populate the global NAV cache so search shows live prices as soon as possible.
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshGlobalNavCacheAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Global NAV cache refresh failed — retrying in 30 minutes.");
                await Task.Delay(TimeSpan.FromMinutes(30), ct);
                continue;
            }

            await Task.Delay(GlobalNavCacheInterval, ct);
        }
    }

    private async Task RefreshGlobalNavCacheAsync(CancellationToken ct)
    {
        using var scope   = _scopeFactory.CreateScope();
        var funds         = scope.ServiceProvider.GetRequiredService<IFundMetaRepository>();
        var allGlobal     = await funds.GetAllByRegionAsync(Region.GLOBAL, ct);
        var tickers       = allGlobal.Select(f => f.Ticker).Distinct().ToList();

        if (tickers.Count == 0)
        {
            _logger.LogInformation("Global NAV cache refresh skipped — no Global funds in fund_meta yet.");
            return;
        }

        await _yahoo.FetchAndCacheGlobalNavsAsync(tickers, ct);
    }

    // ── Warm-up ──────────────────────────────────────────────────────────────────

    private async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            var amfiTask  = WarmUpAmfiAsync(ct);
            var yahooTask = WarmUpYahooAsync(ct);
            await Task.WhenAll(amfiTask, yahooTask);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Warm-up encountered an error.");
        }
    }

    private async Task WarmUpAmfiAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var amfi  = scope.ServiceProvider.GetRequiredService<IAmfiService>();
            var funds = scope.ServiceProvider.GetRequiredService<IFundMetaRepository>();

            var allNavs = await amfi.FetchAllNavsAsync(ct);
            _logger.LogInformation("AMFI warm-up complete — {Count} Growth plan entries cached in memory.", allNavs.Count);

            // Auto-reseed fund_meta if India funds look sparse (< 1000 records).
            // India search now uses the AMFI cache directly, but fund_meta must be populated
            // so that holdings can reference a valid FundId when a user adds a fund.
            var indiaCount = await funds.GetAllByRegionAsync(Region.INDIA, ct)
                                        .ContinueWith(t => t.Result.Count, ct);
            if (indiaCount < 1000)
            {
                _logger.LogWarning(
                    "India fund_meta has only {Count} records — running automatic reseed from AMFI cache.", indiaCount);

                var entities = allNavs.Select(f => new GWT.Domain.Entities.FundMeta
                {
                    Id         = $"IN-{f.SchemeCode}",
                    Region     = Region.INDIA,
                    Name       = f.SchemeName,
                    Amc        = f.Amc,
                    Ticker     = $"AMFI-{f.SchemeCode}",
                    SchemeCode = f.SchemeCode,
                    Isin       = f.Isin,
                    Timezone   = "Asia/Kolkata",
                    LatestNav  = f.Nav,
                    NavDate    = f.NavDate,
                    UpdatedAt  = DateTime.UtcNow,
                });

                await funds.BulkUpsertFundsAsync(entities, ct);
                _logger.LogInformation("Auto-reseed complete: {Count} India funds upserted into fund_meta.", allNavs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AMFI warm-up failed — India search will fall back to on-demand AMFI fetch.");
        }
    }

    private async Task WarmUpYahooAsync(CancellationToken ct)
    {
        try
        {
            await _yahoo.WarmUpAsync(ct);
            _logger.LogInformation("Yahoo Finance warm-up complete — crumb cached.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo Finance warm-up failed — first Global search will be slower.");
        }
    }

    // ── Timezone utilities ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="TimeZoneInfo"/> for the given IANA identifier.
    /// Works on both Windows (which uses Windows IDs) and Linux (Render / Docker).
    /// Falls back to a fixed-offset UTC zone if the ID cannot be resolved.
    /// </summary>
    private TimeZoneInfo ResolveTimeZone(string ianaId)
    {
        // .NET 6+ on Linux accepts IANA IDs directly.
        // On Windows, TryConvertIanaIdToWindowsId handles the mapping.
        if (TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var tz))
            return tz;

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var winId) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(winId, out tz))
            return tz;

        // Known hard-coded fallbacks
        var fixedOffset = ianaId switch
        {
            "Asia/Kolkata"     => TimeSpan.FromHours(5.5),
            "America/New_York" => TimeSpan.FromHours(-5),
            "Europe/London"    => TimeSpan.Zero,
            "Europe/Paris"     => TimeSpan.FromHours(1),
            _                  => TimeSpan.Zero
        };

        _logger.LogWarning(
            "Could not resolve timezone '{IanaId}' — falling back to fixed offset {Offset}.",
            ianaId, fixedOffset);

        return TimeZoneInfo.CreateCustomTimeZone(ianaId, fixedOffset, ianaId, ianaId);
    }

    /// <summary>
    /// Calculates how long to wait until the next midnight in the given timezone.
    /// </summary>
    private static TimeSpan DelayUntilMidnight(TimeZoneInfo tz)
    {
        var nowUtc   = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

        // Next midnight in local time
        var nextMidnightLocal = nowLocal.Date.AddDays(1);

        // Convert back to UTC, accounting for DST transitions
        var nextMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(nextMidnightLocal, DateTimeKind.Unspecified), tz);

        var delay = nextMidnightUtc - nowUtc;

        // Guard: should never be negative, but clamp to avoid instant re-fire
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromHours(24);
    }
}
