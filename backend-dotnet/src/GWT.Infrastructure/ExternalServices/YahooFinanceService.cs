using System.Text.Json;
using GWT.Application;
using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.ExternalServices;

/// <summary>
/// Wraps Yahoo Finance public APIs — no API key required.
/// Uses query2.finance.yahoo.com with crumb-based auth.
///
/// Registered as a singleton so the cookie container (and crumb) survive across
/// DI-created instances. IHttpClientFactory rotates handlers every 2 minutes by
/// default, which would lose session cookies — hence the direct HttpClient here.
/// </summary>
public class YahooFinanceService : IYahooFinanceService
{
    private readonly HttpClient _http;
    private readonly ICacheService _cache;
    private readonly ILogger<YahooFinanceService> _logger;

    // Crumb is tied to the session cookies in _http's handler.
    // volatile so reads/writes are not cached in CPU registers across threads.
    private volatile string? _crumb;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);

    // In-memory search cache — Redis is unavailable on Render free tier so every
    // _cache.GetAsync call is a miss. Caching here avoids a Yahoo HTTP round-trip for
    // repeated searches (e.g. typing one character at a time).
    private readonly Dictionary<string, (List<FundSearchResultDto> Results, DateTime Expiry)> _searchMem
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _searchMemLock = new(1, 1);

    // Process-level global NAV cache — mirrors the AMFI static cache pattern.
    // Written by the 4-hour background refresh; read on every Global search.
    // Static so it survives across transient DI scopes (YahooFinanceService is singleton,
    // but the static keyword makes the intent explicit and guards against future DI changes).
    private static volatile Dictionary<string, YahooQuoteDto>? _globalNavCache;
    private static DateTime _globalNavCacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _globalNavLock = new(1, 1);
    private static readonly TimeSpan GlobalNavCacheTtl = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan QuoteCacheTtl  = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public YahooFinanceService(HttpClient http, ICacheService cache, ILogger<YahooFinanceService> logger)
    {
        _http   = http;
        _cache  = cache;
        _logger = logger;
    }

    // ── Warm-up ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Called at startup to establish the Yahoo Finance session cookie and crumb so
    /// the first real search/quote request doesn't pay the crumb-handshake latency.
    /// </summary>
    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        var crumb = await EnsureCrumbAsync(ct);
        _logger.LogInformation(
            crumb is not null
                ? "Yahoo Finance warm-up complete (crumb acquired)."
                : "Yahoo Finance warm-up: crumb unavailable — will retry on first request.");
    }

    // ── Global NAV cache ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current in-memory global NAV snapshot without triggering a network fetch.
    /// Returns an empty dictionary if the cache has not been populated yet.
    /// </summary>
    public Dictionary<string, YahooQuoteDto> GetGlobalNavSnapshot() =>
        _globalNavCache ?? new Dictionary<string, YahooQuoteDto>(StringComparer.OrdinalIgnoreCase);

    public List<FundSearchResultDto>? TryGetSearchFromCache(string query)
    {
        var memKey = query.ToLowerInvariant();
        if (_searchMem.TryGetValue(memKey, out var entry) && DateTime.UtcNow < entry.Expiry)
            return entry.Results;
        return null;
    }

    /// <summary>
    /// Bulk-fetches live quotes for all provided tickers and stores them in a process-level
    /// static cache with a 4-hour TTL — mirrors the AMFI FetchAllNavsAsync pattern.
    /// Subsequent calls within the TTL return the cached data instantly (no HTTP).
    /// Intended to be called at startup and every 4 hours by the background service.
    /// </summary>
    public async Task<Dictionary<string, YahooQuoteDto>> FetchAndCacheGlobalNavsAsync(
        IEnumerable<string> tickers, CancellationToken ct = default)
    {
        // Fast path — return cached data if still fresh
        if (_globalNavCache is not null && DateTime.UtcNow < _globalNavCacheExpiry)
            return _globalNavCache;

        await _globalNavLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring the lock
            if (_globalNavCache is not null && DateTime.UtcNow < _globalNavCacheExpiry)
                return _globalNavCache;

            var tickerList = tickers.Distinct().ToList();
            if (tickerList.Count == 0)
                return _globalNavCache ?? new Dictionary<string, YahooQuoteDto>(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Refreshing global NAV cache — fetching {Count} tickers from Yahoo Finance.", tickerList.Count);

            var quotes = await GetBulkQuotesAsync(tickerList, ct: ct);

            _globalNavCache = quotes;
            _globalNavCacheExpiry = DateTime.UtcNow.Add(GlobalNavCacheTtl);

            _logger.LogInformation("Global NAV cache refreshed: {Count}/{Total} quotes cached, TTL 4 h.",
                quotes.Count, tickerList.Count);

            return quotes;
        }
        finally
        {
            _globalNavLock.Release();
        }
    }

    public async Task<Dictionary<string, YahooQuoteDto>> ForceRefreshGlobalNavsAsync(
        IEnumerable<string> tickers, CancellationToken ct = default)
    {
        // Reset expiry so FetchAndCacheGlobalNavsAsync bypasses the fast-path and re-fetches.
        _globalNavCacheExpiry = DateTime.MinValue;
        return await FetchAndCacheGlobalNavsAsync(tickers, ct);
    }

    /// <summary>
    /// Merges already-fetched quotes into the global NAV cache and resets the TTL.
    /// Thread-safe via volatile write on the new dictionary reference.
    /// </summary>
    public void MergeGlobalNavCache(Dictionary<string, YahooQuoteDto> quotes)
    {
        if (quotes.Count == 0) return;

        var current = _globalNavCache ?? new Dictionary<string, YahooQuoteDto>(StringComparer.OrdinalIgnoreCase);
        var merged  = new Dictionary<string, YahooQuoteDto>(current, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in quotes) merged[k] = v;

        _globalNavCache        = merged;
        _globalNavCacheExpiry  = DateTime.UtcNow.Add(GlobalNavCacheTtl);

        _logger.LogInformation("Global NAV cache updated via merge: {Count} total entries.", merged.Count);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public async Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var cacheKey = $"yahoo:search:{query.ToLowerInvariant()}";

        // 1. In-memory cache (fastest — no I/O)
        var memKey = query.ToLowerInvariant();
        if (_searchMem.TryGetValue(memKey, out var memEntry) && DateTime.UtcNow < memEntry.Expiry)
            return memEntry.Results;

        // 2. Redis / distributed cache (no-op on Render free tier)
        var cached = await _cache.GetAsync<List<FundSearchResultDto>>(cacheKey, ct);
        if (cached is not null)
        {
            _searchMem[memKey] = (cached, DateTime.UtcNow.Add(SearchCacheTtl));
            return cached;
        }

        var crumb = await EnsureCrumbAsync(ct);
        var crumbParam = crumb is not null ? $"&crumb={Uri.EscapeDataString(crumb)}" : "";
        // Normalise to lowercase — Yahoo Finance is case-insensitive but lowercase
        // avoids any edge cases with crumb-signed requests.
        var normalisedQuery = query.Trim().ToLowerInvariant();
        var url = $"https://query2.finance.yahoo.com/v1/finance/search" +
                  $"?q={Uri.EscapeDataString(normalisedQuery)}&quotesCount=20&newsCount=0" +
                  $"&enableFuzzyQuery=false&region=US&lang=en-US{crumbParam}";

        var json = await SafeGetStringAsync(url, ct);
        if (json is null) return [];

        var results = new List<FundSearchResultDto>();
        try
        {
            using var doc = JsonDocument.Parse(json);

            // Yahoo Finance changed their response shape.
            // New (2025+): { "quotes": [...], "count": N, ... }  — flat root
            // Old:         { "finance": { "result": [{ "quotes": [...] }] } }
            // Support both so a future rollback doesn't break us again.
            JsonElement quotes;
            if (doc.RootElement.TryGetProperty("quotes", out quotes))
            {
                // New flat structure — quotes is at root
            }
            else if (doc.RootElement.TryGetProperty("finance", out var finance) &&
                     finance.TryGetProperty("result", out var resultEl) &&
                     resultEl.ValueKind == JsonValueKind.Array &&
                     resultEl.GetArrayLength() > 0 &&
                     resultEl[0].TryGetProperty("quotes", out quotes))
            {
                // Old nested structure — finance.result[0].quotes
            }
            else
            {
                _logger.LogWarning("Yahoo Finance search response has unexpected shape for query '{Query}'", query);
                return results;
            }

            foreach (var q in quotes.EnumerateArray())
            {
                var type     = q.TryGetProperty("quoteType", out var qt) ? qt.GetString() : null;
                var ticker   = q.TryGetProperty("symbol",    out var sym) ? sym.GetString() : null;
                var name     = q.TryGetProperty("shortname", out var sn)  ? sn.GetString()
                             : q.TryGetProperty("longname",  out var ln)  ? ln.GetString() : ticker;
                // Prefer exchDisp ("NYSEArca") over the raw code ("PCX")
                var exchange = q.TryGetProperty("exchDisp",  out var exd) ? exd.GetString()
                             : q.TryGetProperty("exchange",  out var ex)  ? ex.GetString() : null;

                if (ticker is null) continue;

                results.Add(new FundSearchResultDto(
                    Id:        $"US-{ticker}",
                    Region:    Region.GLOBAL,
                    Name:      name ?? ticker,
                    Amc:       exchange ?? "Yahoo",
                    Ticker:    ticker,
                    SchemeCode: null,
                    Category:  type,
                    LatestNav: null,
                    NavDate:   null,
                    Currency:  CurrencyHelper.GetCurrency(ticker)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Yahoo Finance search response for query '{Query}'", query);
            // Stale crumb may have caused a non-JSON response — reset so next call re-fetches
            if (ex is JsonException) _crumb = null;
        }

        if (results.Count > 0)
        {
            _searchMem[memKey] = (results, DateTime.UtcNow.Add(SearchCacheTtl));
            await _cache.SetAsync(cacheKey, results, SearchCacheTtl, ct);
        }

        return results;
    }

    // ── Quote ─────────────────────────────────────────────────────────────────

    public async Task<YahooQuoteDto?> GetQuoteAsync(string ticker, CancellationToken ct = default)
    {
        var cacheKey = $"yahoo:quote:{ticker.ToUpperInvariant()}";
        var cached = await _cache.GetAsync<YahooQuoteDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var crumb = await EnsureCrumbAsync(ct);
        var crumbParam = crumb is not null ? $"&crumb={Uri.EscapeDataString(crumb)}" : "";
        var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker)}" +
                  $"?interval=1d&range=1d{crumbParam}";

        var json = await SafeGetStringAsync(url, ct);
        if (json is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var meta   = result.GetProperty("meta");
            var price  = meta.TryGetProperty("regularMarketPrice", out var rmp)
                ? rmp.GetDecimal()
                : meta.GetProperty("chartPreviousClose").GetDecimal();
            var currency  = meta.TryGetProperty("currency",       out var cur) ? cur.GetString() : null;
            var shortName = meta.TryGetProperty("shortName",      out var sn)  ? sn.GetString()  : null;
            var exchange  = meta.TryGetProperty("exchangeName",   out var exn) ? exn.GetString() : null;
            var quoteType = meta.TryGetProperty("instrumentType", out var qt)  ? qt.GetString()  : null;

            var quote = new YahooQuoteDto(ticker, shortName, exchange, quoteType, price, currency, DateTime.UtcNow);
            await _cache.SetAsync(cacheKey, quote, QuoteCacheTtl, ct);
            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Yahoo Finance chart response for ticker '{Ticker}'", ticker);
            _crumb = null;
            return null;
        }
    }

    public async Task<Dictionary<string, YahooQuoteDto>> GetBatchQuotesAsync(
        IEnumerable<string> tickers, CancellationToken ct = default)
    {
        var tickerList = tickers.Distinct().ToList();
        var result     = new Dictionary<string, YahooQuoteDto>(StringComparer.OrdinalIgnoreCase);

        var tasks = tickerList.Select(async ticker =>
        {
            var quote = await GetQuoteAsync(ticker, ct);
            if (quote is not null)
                lock (result) { result[ticker] = quote; }
        });

        await Task.WhenAll(tasks);
        return result;
    }

    /// <summary>
    /// Bulk quote fetch via the Yahoo Finance v7/finance/quote batch endpoint.
    /// Sends one HTTP request per chunk of <paramref name="chunkSize"/> tickers
    /// with a <paramref name="delayMs"/> courtesy pause between chunks to stay
    /// well inside Yahoo's unofficial rate limits.
    ///
    /// Response shape: { "quoteResponse": { "result": [ { "symbol": "...",
    ///   "regularMarketPrice": 0.0, "currency": "USD", "shortName": "..." } ] } }
    /// </summary>
    public async Task<Dictionary<string, YahooQuoteDto>> GetBulkQuotesAsync(
        IEnumerable<string> tickers,
        int chunkSize = 100,
        int delayMs   = 300,
        CancellationToken ct = default)
    {
        var tickerList = tickers.Distinct().ToList();
        var result     = new Dictionary<string, YahooQuoteDto>(StringComparer.OrdinalIgnoreCase);
        if (tickerList.Count == 0) return result;

        var crumb = await EnsureCrumbAsync(ct);

        for (int i = 0; i < tickerList.Count; i += chunkSize)
        {
            ct.ThrowIfCancellationRequested();

            var chunk   = tickerList.Skip(i).Take(chunkSize);
            var symbols = string.Join(",", chunk); // tickers are alphanumeric — no encoding needed
            var crumbParam = crumb is not null ? $"&crumb={Uri.EscapeDataString(crumb)}" : "";
            var url     = $"https://query2.finance.yahoo.com/v7/finance/quote?symbols={symbols}{crumbParam}";

            var json = await SafeGetStringAsync(url, ct);
            if (json is not null)
                ParseV7QuoteResponse(json, result);

            // Courtesy delay between chunks — skip on the last iteration
            if (i + chunkSize < tickerList.Count && delayMs > 0)
                await Task.Delay(delayMs, ct);
        }

        _logger.LogDebug(
            "GetBulkQuotesAsync: {Requested} tickers → {Returned} quotes ({Chunks} requests)",
            tickerList.Count, result.Count, (int)Math.Ceiling((double)tickerList.Count / chunkSize));

        return result;
    }

    /// <summary>Parses a Yahoo Finance v7/finance/quote JSON response into the result dictionary.</summary>
    private void ParseV7QuoteResponse(string json, Dictionary<string, YahooQuoteDto> result)
    {
        try
        {
            using var doc          = JsonDocument.Parse(json);
            var quoteResponse      = doc.RootElement.GetProperty("quoteResponse");
            var resultArray        = quoteResponse.GetProperty("result");

            foreach (var item in resultArray.EnumerateArray())
            {
                var symbol = item.TryGetProperty("symbol", out var sym) ? sym.GetString() : null;
                if (symbol is null) continue;

                // Prefer live price; fall back to previous close if market is shut
                decimal price;
                if (item.TryGetProperty("regularMarketPrice", out var rmp) && rmp.ValueKind == JsonValueKind.Number)
                    price = rmp.GetDecimal();
                else if (item.TryGetProperty("regularMarketPreviousClose", out var rmpc) && rmpc.ValueKind == JsonValueKind.Number)
                    price = rmpc.GetDecimal();
                else
                    continue;

                var currency  = item.TryGetProperty("currency",  out var cur) ? cur.GetString() ?? "USD" : "USD";
                var shortName = item.TryGetProperty("shortName", out var sn)  ? sn.GetString()  : null;
                var exchange  = item.TryGetProperty("exchange",  out var ex)  ? ex.GetString()  : null;
                var quoteType = item.TryGetProperty("quoteType", out var qt)  ? qt.GetString()  : null;

                result[symbol] = new YahooQuoteDto(symbol, shortName, exchange, quoteType, price, currency, DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Yahoo Finance v7 quote batch response");
            // Stale crumb may have caused a non-JSON body — reset so next call re-fetches
            if (ex is JsonException) _crumb = null;
        }
    }

    // ── Crumb / session ───────────────────────────────────────────────────────

    /// <summary>
    /// Obtains a Yahoo Finance crumb token.
    /// Step 1: GET https://fc.yahoo.com  — sets session cookies in the shared handler.
    /// Step 2: GET /v1/test/getcrumb     — returns the crumb string.
    /// The crumb must be sent with every subsequent data request as &crumb=…
    /// </summary>
    private async Task<string?> EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumb is not null) return _crumb;

        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumb is not null) return _crumb;

            // Warm up session cookies
            try { await _http.GetAsync("https://fc.yahoo.com", ct); } catch { /* best-effort */ }

            using var response = await _http.GetAsync(
                "https://query2.finance.yahoo.com/v1/test/getcrumb", ct);

            if (response.IsSuccessStatusCode)
            {
                var crumb = (await response.Content.ReadAsStringAsync(ct)).Trim();
                if (!string.IsNullOrWhiteSpace(crumb) && !crumb.StartsWith("<"))
                {
                    _crumb = crumb;
                    _logger.LogInformation("Yahoo Finance crumb obtained");
                    return _crumb;
                }
            }

            _logger.LogWarning(
                "Could not obtain Yahoo Finance crumb (HTTP {Status}) — proceeding without",
                (int)response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while fetching Yahoo Finance crumb");
            return null;
        }
        finally
        {
            _crumbLock.Release();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
