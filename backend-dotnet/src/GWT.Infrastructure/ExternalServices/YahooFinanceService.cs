using System.Text.Json;
using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.ExternalServices;

/// <summary>
/// Wraps Yahoo Finance public APIs — no API key required.
/// Chart endpoint: /v8/finance/chart/{ticker}
/// Search endpoint: /v1/finance/search?q={query}
/// </summary>
public class YahooFinanceService : IYahooFinanceService
{
    private readonly HttpClient _http;
    private readonly ICacheService _cache;
    private readonly ILogger<YahooFinanceService> _logger;

    private static readonly TimeSpan QuoteCacheTtl = TimeSpan.FromHours(4);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public YahooFinanceService(HttpClient http, ICacheService cache, ILogger<YahooFinanceService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var cacheKey = $"yahoo:search:{query.ToLowerInvariant()}";
        var cached = await _cache.GetAsync<List<FundSearchResultDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=20&newsCount=0";
        var json = await SafeGetStringAsync(url, ct);
        if (json is null) return [];

        var results = new List<FundSearchResultDto>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var quotes = doc.RootElement
                .GetProperty("finance")
                .GetProperty("result")[0]
                .GetProperty("quotes");

            foreach (var q in quotes.EnumerateArray())
            {
                var type = q.TryGetProperty("quoteType", out var qt) ? qt.GetString() : null;
                var ticker = q.TryGetProperty("symbol", out var sym) ? sym.GetString() : null;
                var name = q.TryGetProperty("shortname", out var sn) ? sn.GetString()
                         : q.TryGetProperty("longname", out var ln) ? ln.GetString() : ticker;
                var exchange = q.TryGetProperty("exchange", out var ex) ? ex.GetString() : null;

                if (ticker is null) continue;

                results.Add(new FundSearchResultDto(
                    Id: $"US-{ticker}",
                    Region: Region.GLOBAL,
                    Name: name ?? ticker,
                    Amc: exchange ?? "Yahoo",
                    Ticker: ticker,
                    SchemeCode: null,
                    Category: type,
                    LatestNav: null,
                    NavDate: null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Yahoo Finance search response for query '{Query}'", query);
        }

        await _cache.SetAsync(cacheKey, results, SearchCacheTtl, ct);
        return results;
    }

    public async Task<YahooQuoteDto?> GetQuoteAsync(string ticker, CancellationToken ct = default)
    {
        var cacheKey = $"yahoo:quote:{ticker.ToUpperInvariant()}";
        var cached = await _cache.GetAsync<YahooQuoteDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval=1d&range=1d";
        var json = await SafeGetStringAsync(url, ct);
        if (json is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var meta = result.GetProperty("meta");
            var price = meta.TryGetProperty("regularMarketPrice", out var rmp)
                ? rmp.GetDecimal()
                : meta.GetProperty("chartPreviousClose").GetDecimal();
            var currency = meta.TryGetProperty("currency", out var cur) ? cur.GetString() : null;
            var shortName = meta.TryGetProperty("shortName", out var sn) ? sn.GetString() : null;
            var exchange = meta.TryGetProperty("exchangeName", out var ex) ? ex.GetString() : null;
            var quoteType = meta.TryGetProperty("instrumentType", out var qt) ? qt.GetString() : null;

            var quote = new YahooQuoteDto(ticker, shortName, exchange, quoteType, price, currency, DateTime.UtcNow);
            await _cache.SetAsync(cacheKey, quote, QuoteCacheTtl, ct);
            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Yahoo Finance chart response for ticker '{Ticker}'", ticker);
            return null;
        }
    }

    public async Task<Dictionary<string, YahooQuoteDto>> GetBatchQuotesAsync(
        IEnumerable<string> tickers, CancellationToken ct = default)
    {
        var tickerList = tickers.Distinct().ToList();
        var result = new Dictionary<string, YahooQuoteDto>(StringComparer.OrdinalIgnoreCase);

        var tasks = tickerList.Select(async ticker =>
        {
            var quote = await GetQuoteAsync(ticker, ct);
            if (quote is not null)
                lock (result) { result[ticker] = quote; }
        });

        await Task.WhenAll(tasks);
        return result;
    }

    private async Task<string?> SafeGetStringAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _http.GetStringAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP request failed: {Url}", url);
            return null;
        }
    }
}
