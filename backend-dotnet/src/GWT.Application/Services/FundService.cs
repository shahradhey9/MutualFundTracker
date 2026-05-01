using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

// CurrencyHelper is in the GWT.Application namespace — no extra using needed

namespace GWT.Application.Services;

public class FundService : IFundService
{
    private readonly IFundMetaRepository _funds;
    private readonly IAmfiService _amfi;
    private readonly IYahooFinanceService _yahoo;
    private readonly ILogger<FundService> _logger;

    public FundService(
        IFundMetaRepository funds,
        IAmfiService amfi,
        IYahooFinanceService yahoo,
        ILogger<FundService> logger)
    {
        _funds = funds;
        _amfi = amfi;
        _yahoo = yahoo;
        _logger = logger;
    }

    public async Task<List<FundSearchResultDto>> SearchAsync(string query, Region region, CancellationToken ct = default)
    {
        if (region == Region.INDIA)
        {
            // 1. AMFI in-memory cache — ~7000 Growth-plan funds with the freshest NAVs
            //    (updated every 30 min). Falls back to live HTTP only during the cold-start
            //    window before the first fetch completes.
            var memResults = await _amfi.SearchAsync(query, ct);
            if (memResults.Count > 0)
            {
                _logger.LogDebug("India search '{Query}' → {Count} results from AMFI memory cache", query, memResults.Count);
                return memResults;
            }

            // 2. fund_meta DB — catches any fund persisted via EnsureFund that isn't in the
            //    standard AMFI Growth list (e.g. legacy or dividend plans).
            var dbResults = await _funds.SearchAsync(query, region, ct: ct);
            _logger.LogDebug("India search '{Query}' → {Count} results from fund_meta DB (memory miss)", query, dbResults.Count);
            return dbResults.Select(ToSearchResultDto).ToList();
        }
        else // GLOBAL
        {
            var navSnapshot = _yahoo.GetGlobalNavSnapshot();

            // 1. Yahoo in-memory search cache — populated by prior searches, zero I/O.
            //    Overlay with the latest prices from the global NAV snapshot.
            var memResults = _yahoo.TryGetSearchFromCache(query);
            if (memResults is { Count: > 0 })
            {
                _logger.LogDebug("Global search '{Query}' → {Count} results from Yahoo memory cache", query, memResults.Count);
                return memResults.Select(r =>
                    navSnapshot.TryGetValue(r.Ticker, out var q)
                        ? r with { LatestNav = q.Price, NavDate = q.Timestamp }
                        : r).ToList();
            }

            // 2. fund_meta DB — name-searchable catalogue with NAV snapshot overlay.
            var dbResults = await _funds.SearchAsync(query, region, ct: ct);
            if (dbResults.Count > 0)
            {
                _logger.LogDebug("Global search '{Query}' → {Count} results from fund_meta DB", query, dbResults.Count);
                return dbResults.Select(f =>
                    navSnapshot.TryGetValue(f.Ticker, out var q)
                        ? new FundSearchResultDto(f.Id, f.Region, f.Name, f.Amc, f.Ticker, f.SchemeCode, f.Category, q.Price, q.Timestamp, CurrencyHelper.GetCurrency(f.Ticker, f.Timezone))
                        : ToSearchResultDto(f)).ToList();
            }

            // 3. Live Yahoo Finance search — also populates the search cache for next time.
            _logger.LogInformation("Global search '{Query}' — nothing in cache or DB, falling back to live Yahoo.", query);
            return await _yahoo.SearchAsync(query, ct);
        }
    }

    private static FundSearchResultDto ToSearchResultDto(FundMeta f) =>
        new(f.Id, f.Region, f.Name, f.Amc, f.Ticker, f.SchemeCode, f.Category, f.LatestNav, f.NavDate, CurrencyHelper.GetCurrency(f.Ticker, f.Timezone));

    public async Task<FundNavDto> GetNavAsync(string ticker, Region region, CancellationToken ct = default)
    {
        if (region == Region.INDIA)
        {
            var schemeCode = ticker.Replace("AMFI-", string.Empty);
            var nav = await _amfi.GetNavAsync(schemeCode, ct)
                      ?? throw new KeyNotFoundException($"NAV not found for scheme {schemeCode}.");
            return new FundNavDto(ticker, nav, DateTime.UtcNow, "INR");
        }
        else
        {
            var quote = await _yahoo.GetQuoteAsync(ticker, ct)
                        ?? throw new KeyNotFoundException($"Quote not found for ticker {ticker}.");
            return new FundNavDto(ticker, quote.Price, quote.Timestamp, quote.Currency);
        }
    }

    public async Task<FundMetaDto> EnsureFundAsync(EnsureFundRequestDto request, CancellationToken ct = default)
    {
        var existing = await _funds.GetByIdAsync(request.Id, ct);
        if (existing is not null)
        {
            // For global funds, refresh NAV if the stored value is from a previous day.
            // Check the in-memory snapshot (populated by the background service) first —
            // zero HTTP cost.  Fall back to a live Yahoo call only when the ticker isn't
            // in the snapshot yet (e.g. a brand-new fund not yet seen by the background job).
            if (existing.Region == Region.GLOBAL)
            {
                var today = DateTime.UtcNow.Date;
                if (existing.NavDate is null || existing.NavDate.Value.Date < today)
                {
                    var navSnapshot = _yahoo.GetGlobalNavSnapshot();
                    var quote = navSnapshot.TryGetValue(existing.Ticker, out var cached)
                        ? cached
                        : await _yahoo.GetQuoteAsync(existing.Ticker, ct);

                    if (quote is not null)
                    {
                        await _funds.UpdateNavBatchAsync(
                            [(existing.Id, quote.Price, quote.Timestamp)], ct);
                        existing.LatestNav = quote.Price;
                        existing.NavDate   = quote.Timestamp;
                        _logger.LogInformation(
                            "EnsureFund: refreshed stale NAV for {FundId} → {Price} {Currency}",
                            existing.Id, quote.Price, quote.Currency);
                    }
                }
            }
            return ToDto(existing);
        }

        decimal? nav = null;
        DateTime? navDate = null;

        if (request.Region == Region.INDIA && request.SchemeCode is not null)
        {
            nav = await _amfi.GetNavAsync(request.SchemeCode, ct);
            navDate = nav.HasValue ? DateTime.UtcNow : null;
        }
        else if (request.Region == Region.GLOBAL)
        {
            // Check in-memory snapshot first (populated every 30 min by the background service)
            // to avoid a live HTTP call for tickers the background job already knows about.
            var navSnapshot = _yahoo.GetGlobalNavSnapshot();
            var quote = navSnapshot.TryGetValue(request.Ticker, out var cached)
                ? cached
                : await _yahoo.GetQuoteAsync(request.Ticker, ct);

            if (quote is not null)
            {
                nav = quote.Price;
                navDate = quote.Timestamp;
            }
        }

        var fund = new FundMeta
        {
            Id = request.Id,
            Region = request.Region,
            Name = request.Name,
            Amc = request.Amc,
            Ticker = request.Ticker,
            SchemeCode = request.SchemeCode,
            Category = request.Category,
            Isin = request.Isin,
            LatestNav = nav,
            NavDate = navDate,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await _funds.UpsertAsync(fund, ct);
        _logger.LogInformation("Fund ensured in catalogue: {FundId} ({Name})", saved.Id, saved.Name);
        return ToDto(saved);
    }

    private static FundMetaDto ToDto(FundMeta f) =>
        new(f.Id, f.Region, f.Name, f.Amc, f.Ticker, f.SchemeCode, f.Category, f.Isin, f.LatestNav, f.NavDate);
}
