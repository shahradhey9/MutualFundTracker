namespace GWT.Application.DTOs.Funds;

/// <summary>
/// Represents a single ETF entry parsed from the NASDAQ Trader symbol directory files
/// (nasdaqlisted.txt and otherlisted.txt).
/// </summary>
public record NasdaqSymbolDto(string Symbol, string Name, string Exchange);
