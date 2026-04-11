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

    public Task<List<FundSearchResultDto>> SearchAsync(string query, Region region, CancellationToken ct = default) =>
        region == Region.INDIA
            ? _amfi.SearchAsync(query, ct)
            : _yahoo.SearchAsync(query, ct);

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
            return ToDto(existing);

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
