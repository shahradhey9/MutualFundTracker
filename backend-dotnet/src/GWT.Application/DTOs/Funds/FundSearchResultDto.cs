using GWT.Domain.Enums;

namespace GWT.Application.DTOs.Funds;

public record FundSearchResultDto(
    string Id,
    Region Region,
    string Name,
    string Amc,
    string Ticker,
    string? SchemeCode,
    string? Category,
    decimal? LatestNav,
    DateTime? NavDate
);
