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

        // Also run a 30-min cache-refresh loop that keeps both India and Global NAVs
        // fresh in memory AND persists the latest values to fund_meta.
        return Task.WhenAll([..loops, RunNavCacheRefreshLoopAsync(stoppingToken)]);
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

    // ── Combined NAV cache refresh loop (every 30 min) ──────────────────────────

    private static readonly TimeSpan NavCacheRefreshInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Refreshes both India (AMFI) and Global (Yahoo Finance) NAV caches every 30 minutes.
    /// Each refresh updates the in-memory cache AND persists the latest NAVs to fund_meta.
    /// India and Global are run in parallel but with independent error handling — a failure
    /// in one does not prevent the other from completing.
    /// </summary>
    private async Task RunNavCacheRefreshLoopAsync(CancellationToken ct)
    {
        // Wait for startup warm-up to finish before the first refresh.
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        while (!ct.IsCancellationRequested)
        {
            // Run India and Global in parallel, each guarded independently so a failure
            // in one region never silences the other.
            await Task.WhenAll(
                RunWithGuardAsync("India", RefreshIndiaNavCacheAsync, ct),
                RunWithGuardAsync("Global", RefreshGlobalNavCacheAsync, ct));

            try { await Task.Delay(NavCacheRefreshInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Runs <paramref name="refresh"/> and logs any exception without re-throwing,
    /// so a failure in one region's refresh never cancels the sibling task.
    /// </summary>
    private async Task RunWithGuardAsync(
        string region,
        Func<CancellationToken, Task> refresh,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        try
        {
            await refresh(ct);
        }
        catch (OperationCanceledException) { /* shutting down — expected */ }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Region} NAV cache refresh failed — in-memory and fund_meta may be stale until next cycle.",
                region);
        }
    }

    /// <summary>
    /// Force-fetches the latest AMFI NAVAll.txt (bypasses TTL), updates the in-memory
    /// AMFI cache, then bulk-upserts all ~7 000 India fund rows into fund_meta.
    ///
    /// ForceRefreshAsync is used (not FetchAllNavsAsync) so every 30-minute cycle always
    /// pulls a fresh file from AMFI regardless of the cache TTL.  This guarantees that
    /// both the in-memory cache and fund_meta are updated with the same data atomically.
    ///
    /// BulkUpsertFundsAsync (INSERT … ON CONFLICT DO UPDATE) is used instead of
    /// UpdateNavBatchAsync so any fund missing from fund_meta — e.g. a new AMC launch
    /// or a fund not present at initial seed — is automatically inserted.
    /// </summary>
    private async Task RefreshIndiaNavCacheAsync(CancellationToken ct)
    {
        using var scope     = _scopeFactory.CreateScope();
        var amfi            = scope.ServiceProvider.GetRequiredService<IAmfiService>();
        var fundRepo        = scope.ServiceProvider.GetRequiredService<IFundMetaRepository>();
        var portfolioSvc    = scope.ServiceProvider.GetRequiredService<IPortfolioService>();

        // Force-refresh bypasses the TTL and re-fetches from AMFI unconditionally.
        // This also clears _searchMemCache so stale search results are evicted.
        _logger.LogInformation("India NAV refresh: force-fetching AMFI NAVAll.txt …");
        var allNavs = await amfi.ForceRefreshAsync(ct);

        if (allNavs.Count == 0)
        {
            _logger.LogWarning("India NAV refresh: AMFI returned 0 funds — skipping fund_meta update.");
            return;
        }

        _logger.LogInformation(
            "India NAV refresh: AMFI returned {Count} funds — upserting into fund_meta …", allNavs.Count);

        var entities = allNavs.Select(f => new GWT.Domain.Entities.FundMeta
        {
            Id         = $"IN-{f.SchemeCode}",
            Region     = GWT.Domain.Enums.Region.INDIA,
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

        await fundRepo.BulkUpsertFundsAsync(entities, ct);

        // Evict all cached portfolios so the next load for every user picks up the fresh NAVs.
        // Without this, portfolios keep serving pre-computed stale values until their 5-min TTL expires.
        portfolioSvc.InvalidateAllCaches();

        _logger.LogInformation(
            "India NAV refresh complete: {Count} funds updated in memory, fund_meta, and portfolio cache cleared.", allNavs.Count);
    }

    /// <summary>
    /// Fetches live Yahoo Finance quotes for all Global tickers in fund_meta,
    /// updates the in-memory Yahoo NAV cache, and persists the prices to fund_meta.
    /// </summary>
    private async Task RefreshGlobalNavCacheAsync(CancellationToken ct)
    {
        using var scope  = _scopeFactory.CreateScope();
        var fundRepo     = scope.ServiceProvider.GetRequiredService<IFundMetaRepository>();
        var portfolioSvc = scope.ServiceProvider.GetRequiredService<IPortfolioService>();

        var allGlobal = await fundRepo.GetAllByRegionAsync(Region.GLOBAL, ct);
        var tickers   = allGlobal.Select(f => f.Ticker).Distinct().ToList();

        if (tickers.Count == 0)
        {
            _logger.LogInformation("Global NAV refresh skipped — no Global funds in fund_meta yet.");
            return;
        }

        var quotes = await _yahoo.FetchAndCacheGlobalNavsAsync(tickers, ct);

        if (quotes.Count == 0) return;

        // Map ticker → fund_meta id, then persist NAVs.
        var tickerToId = allGlobal
            .Where(f => !string.IsNullOrEmpty(f.Ticker))
            .ToDictionary(f => f.Ticker, f => f.Id, StringComparer.OrdinalIgnoreCase);

        var updates = quotes
            .Where(kv => tickerToId.ContainsKey(kv.Key))
            .Select(kv => (tickerToId[kv.Key], kv.Value.Price, kv.Value.Timestamp));

        await fundRepo.UpdateNavBatchAsync(updates, ct);

        // Evict all cached portfolios so every user sees the updated Global prices immediately.
        portfolioSvc.InvalidateAllCaches();

        _logger.LogInformation(
            "Global NAV refresh complete: {Count} NAVs persisted to fund_meta and portfolio cache cleared.", quotes.Count);
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
