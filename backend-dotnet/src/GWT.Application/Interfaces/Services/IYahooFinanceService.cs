using GWT.Application.DTOs.Funds;

namespace GWT.Application.Interfaces.Services;

public interface IYahooFinanceService
{
    Task<List<FundSearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
    Task<YahooQuoteDto?> GetQuoteAsync(string ticker, CancellationToken ct = default);
    Task<Dictionary<string, YahooQuoteDto>> GetBatchQuotesAsync(IEnumerable<string> tickers, CancellationToken ct = default);
}
