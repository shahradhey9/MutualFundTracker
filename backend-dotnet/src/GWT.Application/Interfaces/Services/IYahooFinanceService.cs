using GWT.Application.DTOs.Funds;

namespace GWT.Application.Interfaces.Services;

public interface IYahooFinanceService
{
    /// <summary>Establishes the Yahoo Finance session cookie and crumb at startup.</summary>
    Task WarmUpAsync(CancellationToken ct = default);
    Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
    Task<YahooQuoteDto?> GetQuoteAsync(string ticker, CancellationToken ct = default);

    /// <summary>
    /// Fetches quotes for a small set of tickers (typically &lt; 50) using individual parallel
    /// GetQuoteAsync calls. Suitable for portfolio refreshes where results are cached per-ticker.
    /// </summary>
    Task<Dictionary<string, YahooQuoteDto>> GetBatchQuotesAsync(IEnumerable<string> tickers, CancellationToken ct = default);

    /// <summary>
    /// Fetches quotes for a large set of tickers using the Yahoo Finance v7 batch endpoint
    /// (/v7/finance/quote?symbols=...), processing in chunks of <paramref name="chunkSize"/>
    /// with a courtesy delay between chunks to avoid rate-limiting.
    /// Use this for bulk catalogue syncs (thousands of tickers).
    /// </summary>
    Task<Dictionary<string, YahooQuoteDto>> GetBulkQuotesAsync(
        IEnumerable<string> tickers,
        int chunkSize = 100,
        int delayMs = 300,
        CancellationToken ct = default);
}
