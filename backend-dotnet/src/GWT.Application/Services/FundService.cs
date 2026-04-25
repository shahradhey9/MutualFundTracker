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
        if (region == Region.INDIA)
        {
            // Search the AMFI in-memory cache directly — it always contains ALL ~7000 Growth
            // plan funds with today's published NAVs, regardless of DB seed state.
            // The DB seed may be incomplete; the AMFI cache is always authoritative for India.
            _logger.LogDebug("India search '{Query}' — using AMFI in-memory cache", query);
            return await _amfi.SearchAsync(query, ct);
        }

        // Global: DB search + overlay live prices from the in-memory Yahoo NAV cache.
        var dbResults = await _funds.SearchAsync(query, region, ct: ct);
        _logger.LogDebug("Global search '{Query}' → {Count} results from fund_meta", query, dbResults.Count);

        if (dbResults.Count > 0)
        {
            var navCache = _yahoo.GetGlobalNavSnapshot();
            return dbResults.Select(f =>
            {
                if (navCache.TryGetValue(f.Ticker, out var q))
                    return new FundSearchResultDto(f.Id, f.Region, f.Name, f.Amc, f.Ticker, f.SchemeCode, f.Category, q.Price, q.Timestamp);
                return ToSearchResultDto(f);
            }).ToList();
        }

        return dbResults.Select(ToSearchResultDto).ToList();
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
