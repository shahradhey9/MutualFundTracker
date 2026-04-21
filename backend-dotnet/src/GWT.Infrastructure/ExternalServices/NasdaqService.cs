using GWT.Application.DTOs.Funds;
using GWT.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.ExternalServices;

/// <summary>
/// Downloads the NASDAQ Trader symbol directory files and extracts all ETFs.
///
/// Two public pipe-delimited files are parsed:
///   nasdaqlisted.txt — NASDAQ-listed securities (ETF column at index 6)
///   otherlisted.txt  — NYSE / NYSE Arca / BATS / IEX listed securities (ETF column at index 4)
///
/// Both are filtered to ETF=Y rows and Test Issue=N rows.
/// Results are cached in a process-level static field for 24 hours — identical pattern to AmfiService.
/// </summary>
public class NasdaqService : INasdaqService
{
    private readonly HttpClient _http;
    private readonly ILogger<NasdaqService> _logger;

    // NASDAQ Trader public symbol directory (HTTP mirror of the FTP feed)
    private const string NasdaqListedUrl = "https://www.nasdaqtrader.com/dynamic/SymDir/nasdaqlisted.txt";
    private const string OtherListedUrl  = "https://www.nasdaqtrader.com/dynamic/SymDir/otherlisted.txt";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    // Process-level static cache — survives across transient DI instances (same pattern as AmfiService).
    private static volatile List<NasdaqSymbolDto>? _memCache;
    private static DateTime _memCacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _fetchLock = new(1, 1);

    public NasdaqService(HttpClient http, ILogger<NasdaqService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<List<NasdaqSymbolDto>> GetAllEtfsAsync(CancellationToken ct = default)
    {
        // Fast path — in-memory static cache
        if (_memCache is not null && DateTime.UtcNow < _memCacheExpiry)
            return _memCache;

        await _fetchLock.WaitAsync(ct);
        try
        {
            // Double-checked locking
            if (_memCache is not null && DateTime.UtcNow < _memCacheExpiry)
                return _memCache;

            _logger.LogInformation("Fetching NASDAQ ETF symbol files from nasdaqtrader.com…");

            // Fetch both files in parallel — they are independent
            var nasdaqTask = FetchAndParseNasdaqListedAsync(ct);
            var otherTask  = FetchAndParseOtherListedAsync(ct);

            await Task.WhenAll(nasdaqTask, otherTask);

            // Deduplicate by symbol in case a ticker appears in both files
            var combined = nasdaqTask.Result
                .Concat(otherTask.Result)
                .GroupBy(e => e.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            _memCache       = combined;
            _memCacheExpiry = DateTime.UtcNow.Add(CacheTtl);

            _logger.LogInformation(
                "NASDAQ ETF catalogue loaded: {Nasdaq} NASDAQ + {Other} other-listed = {Total} unique ETFs",
                nasdaqTask.Result.Count, otherTask.Result.Count, combined.Count);

            return combined;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    // ── Fetch helpers ─────────────────────────────────────────────────────────

    private async Task<List<NasdaqSymbolDto>> FetchAndParseNasdaqListedAsync(CancellationToken ct)
    {
        var text = await SafeGetStringAsync(NasdaqListedUrl, ct);
        return text is null ? [] : ParseNasdaqListed(text);
    }

    private async Task<List<NasdaqSymbolDto>> FetchAndParseOtherListedAsync(CancellationToken ct)
    {
        var text = await SafeGetStringAsync(OtherListedUrl, ct);
        return text is null ? [] : ParseOtherListed(text);
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses nasdaqlisted.txt.
    /// Header: Symbol|Security Name|Market Category|Test Issue|Financial Status|Round Lot Size|ETF|NextShares
    /// ETF column = index 6, Test Issue = index 3.
    /// Last line is a "File Creation Time: …" trailer — skipped by the content check.
    /// </summary>
    private static List<NasdaqSymbolDto> ParseNasdaqListed(string text)
    {
        var results = new List<NasdaqSymbolDto>();
        var lines   = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines.Skip(1)) // skip header
        {
            var line = rawLine.Trim();
            if (line.StartsWith("File Creation Time", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Split('|');
            if (parts.Length < 7) continue;

            var symbol    = parts[0].Trim();
            var name      = parts[1].Trim();
            var testIssue = parts[3].Trim();
            var isEtf     = parts[6].Trim();

            if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(name)) continue;
            if (testIssue.Equals("Y", StringComparison.OrdinalIgnoreCase)) continue;
            if (!isEtf.Equals("Y", StringComparison.OrdinalIgnoreCase)) continue;

            results.Add(new NasdaqSymbolDto(symbol, name, "NASDAQ"));
        }

        return results;
    }

    /// <summary>
    /// Parses otherlisted.txt.
    /// Header: ACT Symbol|Security Name|Exchange|CQS Symbol|ETF|Round Lot Size|Test Issue|NASDAQ Symbol
    /// ETF column = index 4, Test Issue = index 6, Exchange = index 2.
    /// Exchange codes: A=NYSE American, N=NYSE, P=NYSE Arca, Z=BATS, V=IEX.
    /// </summary>
    private static List<NasdaqSymbolDto> ParseOtherListed(string text)
    {
        var results = new List<NasdaqSymbolDto>();
        var lines   = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines.Skip(1)) // skip header
        {
            var line = rawLine.Trim();
            if (line.StartsWith("File Creation Time", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Split('|');
            if (parts.Length < 7) continue;

            var symbol    = parts[0].Trim();
            var name      = parts[1].Trim();
            var exchange  = parts[2].Trim();
            var isEtf     = parts[4].Trim();
            var testIssue = parts[6].Trim();

            if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(name)) continue;
            if (testIssue.Equals("Y", StringComparison.OrdinalIgnoreCase)) continue;
            if (!isEtf.Equals("Y", StringComparison.OrdinalIgnoreCase)) continue;

            var exchangeName = exchange switch
            {
                "A" => "NYSE American",
                "N" => "NYSE",
                "P" => "NYSE Arca",
                "Z" => "BATS",
                "V" => "IEX",
                _   => "Global",
            };

            results.Add(new NasdaqSymbolDto(symbol, name, exchangeName));
        }

        return results;
    }

    private async Task<string?> SafeGetStringAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _http.GetStringAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch NASDAQ symbol file: {Url}", url);
            return null;
        }
    }
}
