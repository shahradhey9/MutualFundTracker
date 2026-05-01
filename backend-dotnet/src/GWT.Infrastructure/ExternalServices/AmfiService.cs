using System.Collections.Concurrent;
using System.Globalization;
using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.ExternalServices;

/// <summary>
/// Fetches and parses the AMFI NAVAll.txt flat file.
/// The file is pipe-delimited: SchemeCode|ISINGrowth|ISINDividend|SchemeName|NAV|Date
/// with AMC header rows and category sub-headers in between.
///
/// Because AmfiService is registered as a typed HttpClient (transient), Redis is the
/// intended cache layer.  On Render free tier Redis is unavailable, so we add a
/// process-level static cache as a fallback so the 2 MB file is fetched at most once
/// per 4-hour window regardless of Redis availability.
/// </summary>
public class AmfiService : IAmfiService
{
    private readonly HttpClient _http;
    private readonly ICacheService _cache;
    private readonly ILogger<AmfiService> _logger;

    private const string NavAllUrl = "https://www.amfiindia.com/spages/NAVAll.txt";
    private const string CacheKeyAll = "amfi:navall";
    private const string CacheKeySearch = "amfi:search:";
    private static readonly TimeSpan NavCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(24);

    // Process-level static cache — survives across transient DI instances.
    // Written once and read many times, so volatile + lock on write is sufficient.
    private static volatile List<AmfiFundRawDto>? _memCache;
    private static DateTime _memCacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _fetchLock = new(1, 1);

    // In-memory search result cache — keyed by lower-cased query string.
    // Eliminates the 7000-item linear scan for repeated / debounced queries.
    // Cleared whenever the nav cache is refreshed so results never go stale.
    private static readonly ConcurrentDictionary<string, List<FundSearchResultDto>> _searchMemCache
        = new(StringComparer.OrdinalIgnoreCase);

    public AmfiService(HttpClient http, ICacheService cache, ILogger<AmfiService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var queryKey = query.Trim().ToLowerInvariant();

        // 1. Process-level in-memory search cache (fastest — zero I/O, zero iteration).
        //    Populated on first search, cleared whenever the nav cache refreshes.
        if (_searchMemCache.TryGetValue(queryKey, out var memHit))
            return memHit;

        var allNavs = await FetchAllNavsAsync(ct);

        // 2. Word-by-word scan — every word must appear in the fund name, AMC, or scheme code.
        //    "hdfc mid cap" matches "HDFC Mid-Cap Opportunities Fund - Direct Growth".
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var results = allNavs
            .Where(f => MatchesAllWords(f, words))
            .Take(50)
            .Select(f => new FundSearchResultDto(
                Id: $"IN-{f.SchemeCode}",
                Region: Region.INDIA,
                Name: f.SchemeName,
                Amc: f.Amc,
                Ticker: $"AMFI-{f.SchemeCode}",
                SchemeCode: f.SchemeCode,
                Category: null,
                LatestNav: f.Nav,
                NavDate: f.NavDate,
                Currency: "INR"))
            .ToList();

        // 3. Store in process-level cache and attempt Redis (no-op on Render free tier).
        _searchMemCache[queryKey] = results;
        await _cache.SetAsync(CacheKeySearch + queryKey, results, SearchCacheTtl, ct);
        return results;
    }

    /// <summary>
    /// Returns true when every word in <paramref name="words"/> appears anywhere in the
    /// fund's scheme name, AMC name, or scheme code (case-insensitive).
    /// </summary>
    private static bool MatchesAllWords(AmfiFundRawDto f, string[] words)
    {
        foreach (var word in words)
        {
            if (!f.SchemeName.Contains(word, StringComparison.OrdinalIgnoreCase) &&
                !f.Amc.Contains(word, StringComparison.OrdinalIgnoreCase) &&
                !f.SchemeCode.Contains(word, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public async Task<List<AmfiFundRawDto>> ForceRefreshAsync(CancellationToken ct = default)
    {
        // Reset expiry so FetchAllNavsAsync bypasses the fast-path and re-fetches from AMFI.
        _memCacheExpiry = DateTime.MinValue;
        return await FetchAllNavsAsync(ct);
    }

    public async Task<decimal?> GetNavAsync(string schemeCode, CancellationToken ct = default)
    {
        var allNavs = await FetchAllNavsAsync(ct);
        var fund = allNavs.FirstOrDefault(f => f.SchemeCode == schemeCode);
        return fund?.Nav;
    }

    public async Task<List<AmfiFundRawDto>> FetchAllNavsAsync(CancellationToken ct = default)
    {
        // 1. Process-level static cache — fastest path, survives DI transient churn.
        if (_memCache is not null && DateTime.UtcNow < _memCacheExpiry)
            return _memCache;

        // 2. Redis / distributed cache (no-op on Render free tier but cheap to check).
        var cached = await _cache.GetAsync<List<AmfiFundRawDto>>(CacheKeyAll, ct);
        if (cached is not null)
        {
            _memCache = cached;
            _memCacheExpiry = DateTime.UtcNow.Add(NavCacheTtl);
            return cached;
        }

        // 3. Fetch from AMFI — serialize via lock so only one thread hits the network.
        await _fetchLock.WaitAsync(ct);
        try
        {
            // Double-checked locking after acquiring the semaphore.
            if (_memCache is not null && DateTime.UtcNow < _memCacheExpiry)
                return _memCache;

            _logger.LogInformation("Fetching AMFI NAVAll.txt from {Url}", NavAllUrl);
            var text = await _http.GetStringAsync(NavAllUrl, ct);
            var parsed = ParseNavAll(text);

            _memCache = parsed;
            _memCacheExpiry = DateTime.UtcNow.Add(NavCacheTtl);

            // Evict stale search results — they reference NAVs from the old data.
            _searchMemCache.Clear();

            await _cache.SetAsync(CacheKeyAll, parsed, NavCacheTtl, ct);
            _logger.LogInformation("AMFI NAVAll.txt parsed: {Count} Growth plan entries", parsed.Count);
            return parsed;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    // ── Parsing ──────────────────────────────────────────────────────────────

    private static List<AmfiFundRawDto> ParseNavAll(string text)
    {
        var results = new List<AmfiFundRawDto>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string currentAmc = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // AMC header lines look like "Aditya Birla Sun Life Mutual Fund"
            // Data lines have semicolons: Code;ISIN;ISIN;Name;NAV;Date
            if (!line.Contains(';'))
            {
                currentAmc = line;
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 6) continue;

            var schemeCode = parts[0].Trim();
            var schemeName = parts[3].Trim();
            var navStr = parts[4].Trim();
            var dateStr = parts[5].Trim();

            // Keep Growth plans only — filter out IDCW / Dividend / Bonus variants
            if (IsNonGrowthPlan(schemeName)) continue;

            if (!decimal.TryParse(navStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var nav))
                continue;

            if (!DateTime.TryParseExact(dateStr, "dd-MMM-yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var navDate))
                continue;

            // SpecifyKind(Utc) is required because DateTime.TryParseExact with DateTimeStyles.None
            // produces DateTimeKind.Unspecified, and Npgsql 6+ throws when writing an Unspecified
            // DateTime to a "timestamp with time zone" column.  All AMFI dates are IST (UTC+5:30)
            // but we treat them as UTC-date-only values for storage purposes — the time component
            // is midnight and the date itself is what matters.
            results.Add(new AmfiFundRawDto(
                SchemeCode: schemeCode,
                SchemeName: schemeName,
                Amc: currentAmc,
                Isin: parts[1].Trim().Length > 0 ? parts[1].Trim() : null,
                Nav: nav,
                NavDate: DateTime.SpecifyKind(navDate, DateTimeKind.Utc)));
        }

        return results;
    }

    private static bool IsNonGrowthPlan(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper.Contains("IDCW") ||
               upper.Contains("DIVIDEND") ||
               upper.Contains("BONUS") ||
               upper.Contains("WEEKLY") ||
               upper.Contains("MONTHLY") ||
               upper.Contains("ANNUAL");
    }
}
