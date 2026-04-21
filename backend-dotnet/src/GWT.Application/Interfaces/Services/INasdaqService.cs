using GWT.Application.DTOs.Funds;

namespace GWT.Application.Interfaces.Services;

/// <summary>
/// Fetches and caches the list of ETFs from the NASDAQ Trader symbol directory.
/// Equivalent to IAmfiService for the India region — used for the global fund catalogue.
/// </summary>
public interface INasdaqService
{
    /// <summary>
    /// Returns all ETFs listed on NASDAQ and other US exchanges (NYSE, NYSE Arca, BATS, IEX).
    /// Results are cached in memory for 24 hours.
    /// </summary>
    Task<List<NasdaqSymbolDto>> GetAllEtfsAsync(CancellationToken ct = default);
}
