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
/// </summary>
public class AmfiService : IAmfiService
{
    private readonly HttpClient _http;
    private readonly ICacheService _cache;
    private readonly ILogger<AmfiService> _logger;

    private const string NavAllUrl = "https://www.amfiindia.com/spages/NAVAll.txt";
    private const string CacheKeyAll = "amfi:navall";
    private const string CacheKeySearch = "amfi:search:";
    private static readonly TimeSpan NavCacheTtl = TimeSpan.FromHours(4);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(24);

    public AmfiService(HttpClient http, ICacheService cache, ILogger<AmfiService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var cacheKey = CacheKeySearch + query.ToLowerInvariant();
        var cached = await _cache.GetAsync<List<FundSearchResultDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var allNavs = await FetchAllNavsAsync(ct);
        var lower = query.ToLowerInvariant();

        var results = allNavs
            .Where(f =>
                f.SchemeName.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                f.Amc.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                f.SchemeCode.Contains(lower, StringComparison.OrdinalIgnoreCase))
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
                NavDate: f.NavDate))
            .ToList();

        await _cache.SetAsync(cacheKey, results, SearchCacheTtl, ct);
        return results;
    }

    public async Task<decimal?> GetNavAsync(string schemeCode, CancellationToken ct = default)
    {
        var allNavs = await FetchAllNavsAsync(ct);
        var fund = allNavs.FirstOrDefault(f => f.SchemeCode == schemeCode);
        return fund?.Nav;
    }

    public async Task<List<AmfiFundRawDto>> FetchAllNavsAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<List<AmfiFundRawDto>>(CacheKeyAll, ct);
        if (cached is not null) return cached;

        _logger.LogInformation("Fetching AMFI NAVAll.txt from {Url}", NavAllUrl);
        var text = await _http.GetStringAsync(NavAllUrl, ct);
        var parsed = ParseNavAll(text);

        await _cache.SetAsync(CacheKeyAll, parsed, NavCacheTtl, ct);
        _logger.LogInformation("AMFI NAVAll.txt parsed: {Count} Growth plan entries", parsed.Count);
        return parsed;
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

            results.Add(new AmfiFundRawDto(
                SchemeCode: schemeCode,
                SchemeName: schemeName,
                Amc: currentAmc,
                Isin: parts[1].Trim().Length > 0 ? parts[1].Trim() : null,
                Nav: nav,
                NavDate: navDate));
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
