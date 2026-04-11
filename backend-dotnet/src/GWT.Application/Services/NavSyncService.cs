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

        var heldFunds = await _funds.GetAllHeldFundsAsync(ct);
        if (heldFunds.Count == 0)
        {
            _logger.LogInformation("NAV sync: no held funds found, skipping.");
            return;
        }

        var indiaFunds = heldFunds.Where(f => f.Region == Region.INDIA).ToList();
        var globalFunds = heldFunds.Where(f => f.Region == Region.GLOBAL).ToList();

        var navUpdates = new List<(string FundId, decimal Nav, DateTime NavDate)>();
        var historyEntries = new List<NavHistory>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── India: parse the full AMFI file once ───────────────────────────
        if (indiaFunds.Count > 0)
        {
            var allAmfiNavs = await _amfi.FetchAllNavsAsync(ct);
            var amfiIndex = allAmfiNavs.ToDictionary(x => x.SchemeCode, x => x);

            foreach (var fund in indiaFunds)
            {
                if (fund.SchemeCode is null) continue;
                if (!amfiIndex.TryGetValue(fund.SchemeCode, out var raw)) continue;

                navUpdates.Add((fund.Id, raw.Nav, raw.NavDate));
                historyEntries.Add(new NavHistory
                {
                    Id = Guid.NewGuid(),
                    FundId = fund.Id,
                    Nav = raw.Nav,
                    NavDate = DateOnly.FromDateTime(raw.NavDate)
                });
            }
        }

        // ── Global: batch-fetch from Yahoo ─────────────────────────────────
        if (globalFunds.Count > 0)
        {
            var tickers = globalFunds.Select(f => f.Ticker);
            var quotes = await _yahoo.GetBatchQuotesAsync(tickers, ct);

            foreach (var fund in globalFunds)
            {
                if (!quotes.TryGetValue(fund.Ticker, out var q)) continue;

                navUpdates.Add((fund.Id, q.Price, q.Timestamp));
                historyEntries.Add(new NavHistory
                {
                    Id = Guid.NewGuid(),
                    FundId = fund.Id,
                    Nav = q.Price,
                    NavDate = today
                });
            }
        }

        // ── Persist ────────────────────────────────────────────────────────
        await _funds.UpdateNavBatchAsync(navUpdates, ct);
        await _navHistory.UpsertBatchAsync(historyEntries, ct);

        // Bust all portfolio cache keys
        await _cache.DeleteByPatternAsync("portfolio:*", ct);

        sw.Stop();
        _logger.LogInformation(
            "NAV sync completed: {Updated} funds updated in {Elapsed}ms",
            navUpdates.Count, sw.ElapsedMilliseconds);
    }
}
