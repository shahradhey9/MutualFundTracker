namespace GWT.Application.DTOs.Funds;

public record FundNavDto(string Ticker, decimal Nav, DateTime NavDate, string? Currency);
