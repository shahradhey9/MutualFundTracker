using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GWT.Application.Services;

public class NavSyncService : INavSyncService
{
    private readonly IFundMetaRepository _funds;
    private readonly INavHistoryRepository _navHistory;
    private readonly IAmfiService _amfi;
    private readonly IYahooFinanceService _yahoo;
    private readonly ICacheService _cache;
    private readonly ILogger<NavSyncService> _logger;

    public NavSyncService(
        IFundMetaRepository funds,
        INavHistoryRepository navHistory,
        IAmfiService amfi,
        IYahooFinanceService yahoo,
        ICacheService cache,
        ILogger<NavSyncService> logger)
    {
        _funds = funds;
        _navHistory = navHistory;
        _amfi = amfi;
        _yahoo = yahoo;
        _cache = cache;
        _logger = logger;
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    public async Task SyncIndiaAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("India NAV sync started at {Time} UTC", DateTime.UtcNow);

        var allFunds = await _funds.GetAllByRegionAsync(Region.INDIA, ct);
        if (allFunds.Count == 0)
        {
            _logger.LogInformation("India NAV sync: fund_meta has no India funds — skipping.");
            return;
        }

        var heldIds = await GetHeldIdsAsync(ct);
        var allAmfiNavs = await _amfi.FetchAllNavsAsync(ct);
        var amfiIndex = allAmfiNavs.ToDictionary(x => x.SchemeCode, x => x);

        var navUpdates = new List<(string FundId, decimal Nav, DateTime NavDate)>();
        var historyEntries = new List<NavHistory>();

        foreach (var fund in allFunds)
        {
            if (fund.SchemeCode is null) continue;
            if (!amfiIndex.TryGetValue(fund.SchemeCode, out var raw)) continue;

            navUpdates.Add((fund.Id, raw.Nav, raw.NavDate));

            if (heldIds.Contains(fund.Id))
                historyEntries.Add(new NavHistory
                {
                    Id      = Guid.NewGuid(),
                    FundId  = fund.Id,
                    Nav     = raw.Nav,
                    NavDate = DateOnly.FromDateTime(raw.NavDate)
                });
        }

        await PersistUpdatesAsync(navUpdates, historyEntries, ct);

        sw.Stop();
        _logger.LogInformation(
            "India NAV sync complete: {Updated} funds, {History} history entries in {Elapsed}ms",
            navUpdates.Count, historyEntries.Count, sw.ElapsedMilliseconds);
    }

    public async Task SyncGlobalByTimezoneAsync(string timezone, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Global NAV sync started for timezone {Timezone} at {Time} UTC", timezone, DateTime.UtcNow);

        var allFunds = await _funds.GetGlobalByTimezoneAsync(timezone, ct);
        if (allFunds.Count == 0)
        {
            _logger.LogInformation("Global NAV sync ({Timezone}): no funds found — skipping.", timezone);
            return;
        }

        var heldIds = await GetHeldIdsAsync(ct);
        var tickers = allFunds.Select(f => f.Ticker);
        var quotes = await _yahoo.GetBulkQuotesAsync(tickers, ct: ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var navUpdates = new List<(string FundId, decimal Nav, DateTime NavDate)>();
        var historyEntries = new List<NavHistory>();

        foreach (var fund in allFunds)
        {
            if (!quotes.TryGetValue(fund.Ticker, out var q)) continue;

            navUpdates.Add((fund.Id, q.Price, q.Timestamp));

            if (heldIds.Contains(fund.Id))
                historyEntries.Add(new NavHistory
                {
                    Id      = Guid.NewGuid(),
                    FundId  = fund.Id,
                    Nav     = q.Price,
                    NavDate = today
                });
        }

        await PersistUpdatesAsync(navUpdates, historyEntries, ct);

        // Keep the in-memory search cache in sync with what we just fetched —
        // avoids the 4-hour refresh loop having to re-fetch the same quotes.
        _yahoo.MergeGlobalNavCache(quotes);

        sw.Stop();
        _logger.LogInformation(
            "Global NAV sync ({Timezone}) complete: {Updated} funds, {History} history entries in {Elapsed}ms",
            timezone, navUpdates.Count, historyEntries.Count, sw.ElapsedMilliseconds);
    }

    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        // India and each global timezone can run concurrently — each uses its own DB context
        // scope (via the repository) and a different external API (AMFI vs Yahoo).
        var distinctTimezones = await GetDistinctGlobalTimezonesAsync(ct);
        var globalTasks = distinctTimezones.Select(tz => SyncGlobalByTimezoneAsync(tz, ct));
        await Task.WhenAll([SyncIndiaAsync(ct), ..globalTasks]);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> GetHeldIdsAsync(CancellationToken ct)
    {
        var heldFunds = await _funds.GetAllHeldFundsAsync(ct);
        return heldFunds.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetDistinctGlobalTimezonesAsync(CancellationToken ct)
    {
        // Fetch all global funds and extract distinct timezones
        var all = await _funds.GetAllByRegionAsync(Region.GLOBAL, ct);
        return all.Select(f => f.Timezone ?? "America/New_York")
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToList();
    }

    private async Task PersistUpdatesAsync(
        List<(string FundId, decimal Nav, DateTime NavDate)> navUpdates,
        List<NavHistory> historyEntries,
        CancellationToken ct)
    {
        await _funds.UpdateNavBatchAsync(navUpdates, ct);
        await _navHistory.UpsertBatchAsync(historyEntries, ct);
        await _cache.DeleteByPatternAsync("portfolio:*", ct);
    }
}
