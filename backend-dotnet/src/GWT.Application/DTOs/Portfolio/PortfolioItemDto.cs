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
    string Currency,         // "INR" for India holdings, "USD" (or quote currency) for Global
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
