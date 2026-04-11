namespace GWT.Application.DTOs.Funds;

public record YahooQuoteDto(
    string Ticker,
    string? ShortName,
    string? Exchange,
    string? QuoteType,
    decimal Price,
    string? Currency,
    DateTime Timestamp
);
