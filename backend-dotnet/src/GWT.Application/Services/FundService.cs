using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

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
        var today = DateTime.UtcNow.Date;

        if (region == Region.INDIA)
        {
            // DB-first: after startup bulk import all India funds are in fund_meta with today's NAV.
            // If the DB has fresh results for this query return them directly without hitting AMFI.
            var dbResults = await _funds.SearchAsync(query, Region.INDIA, ct: ct);
            var freshResults = dbResults
                .Where(f => f.NavDate.HasValue && f.NavDate.Value.Date >= today)
                .ToList();

            if (freshResults.Count > 0)
            {
                _logger.LogDebug("India search '{Query}' served from fund_meta ({Count} results)", query, freshResults.Count);
                return freshResults.Select(ToSearchResultDto).ToList();
            }

            // Fallback: AMFI in-memory cache (always available after warm-up).
            // This path runs on first startup before the bulk import completes,
            // or for edge-case funds not yet imported.
            _logger.LogDebug("India search '{Query}' falling back to AMFI API", query);
            return await _amfi.SearchAsync(query, ct);
        }
        else
        {
            // DB-first for Global: if we have matching funds already cached in fund_meta, return
            // them without a Yahoo round-trip. NAV may be null from search results but that's OK —
            // AddHoldingForm fetches NAV separately via /funds/nav/{ticker}.
            var dbResults = await _funds.SearchAsync(query, Region.GLOBAL, ct: ct);
            if (dbResults.Count > 0)
            {
                _logger.LogDebug("Global search '{Query}' served from fund_meta ({Count} results)", query, dbResults.Count);
                return dbResults.Select(ToSearchResultDto).ToList();
            }

            // Fallback to Yahoo; persist the returned funds so future searches hit the DB.
            _logger.LogDebug("Global search '{Query}' falling back to Yahoo Finance", query);
            var yahooResults = await _yahoo.SearchAsync(query, ct);

            if (yahooResults.Count > 0)
                await PersistGlobalSearchResultsAsync(yahooResults, ct);

            return yahooResults;
        }
    }

    private async Task PersistGlobalSearchResultsAsync(List<FundSearchResultDto> results, CancellationToken ct)
    {
        var funds = results.Select(r => new FundMeta
        {
            Id        = r.Id,
            Region    = Region.GLOBAL,
            Name      = r.Name,
            Amc       = r.Amc,
            Ticker    = r.Ticker,
            Category  = r.Category,
            UpdatedAt = DateTime.UtcNow,
            // LatestNav and NavDate intentionally omitted —
            // Yahoo search results don't include price; NAV is fetched on demand.
        });

        try
        {
            await _funds.BulkUpsertFundsAsync(funds, ct);
            _logger.LogDebug("Persisted {Count} global search results to fund_meta", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist global search results to fund_meta — non-fatal");
        }
    }

    private static FundSearchResultDto ToSearchResultDto(FundMeta f) =>
        new(f.Id, f.Region, f.Name, f.Amc, f.Ticker, f.SchemeCode, f.Category, f.LatestNav, f.NavDate);

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
            // EnsureFund is called every time a holding is added so this keeps the catalogue
            // current without waiting for the daily NavSyncBackgroundService.
            if (existing.Region == Region.GLOBAL)
            {
                var today = DateTime.UtcNow.Date;
                if (existing.NavDate is null || existing.NavDate.Value.Date < today)
                {
                    var quote = await _yahoo.GetQuoteAsync(existing.Ticker, ct);
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
            var quote = await _yahoo.GetQuoteAsync(request.Ticker, ct);
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
