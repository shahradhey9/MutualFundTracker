using GWT.Application.DTOs.Funds;

namespace GWT.Application.DTOs.Portfolio;

public record AddHoldingRequestDto(
    string FundId,
    decimal Units,
    decimal? AvgCost,
    DateTime PurchaseAt,
    // Optional — when present the fund is upserted in the same request so the
    // client needs only one round-trip instead of /funds/ensure + /portfolio/holdings.
    EnsureFundRequestDto? Fund = null
);
