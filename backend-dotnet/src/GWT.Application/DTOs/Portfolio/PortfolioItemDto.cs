using GWT.Domain.Enums;

namespace GWT.Application.DTOs.Portfolio;

public record PortfolioItemDto(
    Guid HoldingId,
    string FundId,
    string Name,
    string Amc,
    string Ticker,
    string? Category,
    Region Region,
    decimal Units,
    decimal? AvgCost,
    decimal? LiveNav,
    decimal? CurrentValue,
    decimal? CostBasis,
    decimal? Gain,
    decimal? GainPct,
    DateTime PurchaseAt,
    DateTime? NavDate
);
