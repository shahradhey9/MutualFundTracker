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

    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("NAV sync started at {Time}", DateTime.UtcNow);

        // Determine which funds have active holdings — NavHistory is written only for these
        // to avoid creating thousands of daily history rows for funds nobody holds.
        var heldFunds = await _funds.GetAllHeldFundsAsync(ct);
        var heldIds   = heldFunds.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Sync ALL funds in fund_meta (not just held ones) so the catalogue stays fresh.
        var allIndiaFunds  = await _funds.GetAllByRegionAsync(Region.INDIA, ct);
        var allGlobalFunds = await _funds.GetAllByRegionAsync(Region.GLOBAL, ct);

        if (allIndiaFunds.Count == 0 && allGlobalFunds.Count == 0)
        {
            _logger.LogInformation("NAV sync: fund_meta is empty — skipping.");
            return;
        }

        var navUpdates     = new List<(string FundId, decimal Nav, DateTime NavDate)>();
        var historyEntries = new List<NavHistory>();
        var today          = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── India: parse the full AMFI file once, update every India fund in fund_meta ──
        if (allIndiaFunds.Count > 0)
        {
            var allAmfiNavs = await _amfi.FetchAllNavsAsync(ct);
            var amfiIndex   = allAmfiNavs.ToDictionary(x => x.SchemeCode, x => x);

            foreach (var fund in allIndiaFunds)
            {
                if (fund.SchemeCode is null) continue;
                if (!amfiIndex.TryGetValue(fund.SchemeCode, out var raw)) continue;

                navUpdates.Add((fund.Id, raw.Nav, raw.NavDate));

                // NavHistory only for held funds
                if (heldIds.Contains(fund.Id))
                    historyEntries.Add(new NavHistory
                    {
                        Id      = Guid.NewGuid(),
                        FundId  = fund.Id,
                        Nav     = raw.Nav,
                        NavDate = DateOnly.FromDateTime(raw.NavDate)
                    });
            }

            _logger.LogInformation("NAV sync: queued {Count} India fund updates", navUpdates.Count);
        }

        // ── Global: batch-fetch from Yahoo for every global fund in fund_meta ──────────
        if (allGlobalFunds.Count > 0)
        {
            var tickers = allGlobalFunds.Select(f => f.Ticker);
            var quotes  = await _yahoo.GetBatchQuotesAsync(tickers, ct);

            var globalUpdated = 0;
            foreach (var fund in allGlobalFunds)
            {
                if (!quotes.TryGetValue(fund.Ticker, out var q)) continue;

                navUpdates.Add((fund.Id, q.Price, q.Timestamp));
                globalUpdated++;

                // NavHistory only for held funds
                if (heldIds.Contains(fund.Id))
                    historyEntries.Add(new NavHistory
                    {
                        Id      = Guid.NewGuid(),
                        FundId  = fund.Id,
                        Nav     = q.Price,
                        NavDate = today
                    });
            }

            _logger.LogInformation("NAV sync: queued {Count} global fund updates", globalUpdated);
        }

        // ── Persist ────────────────────────────────────────────────────────────────────
        await _funds.UpdateNavBatchAsync(navUpdates, ct);
        await _navHistory.UpsertBatchAsync(historyEntries, ct);

        // Bust all portfolio cache keys so the refreshed NAVs are visible immediately
        await _cache.DeleteByPatternAsync("portfolio:*", ct);

        sw.Stop();
        _logger.LogInformation(
            "NAV sync completed: {Updated} funds updated, {History} history entries written in {Elapsed}ms",
            navUpdates.Count, historyEntries.Count, sw.ElapsedMilliseconds);
    }
}
