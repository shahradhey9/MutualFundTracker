using GWT.Domain.Enums;

namespace GWT.Application.DTOs.Funds;

public record FundMetaDto(
    string Id,
    Region Region,
    string Name,
    string Amc,
    string Ticker,
    string? SchemeCode,
    string? Category,
    string? Isin,
    decimal? LatestNav,
    DateTime? NavDate
);
