namespace GWT.Application.DTOs.Portfolio;

public record AddHoldingRequestDto(
    string FundId,
    decimal Units,
    decimal? AvgCost,
    DateTime PurchaseAt
);
