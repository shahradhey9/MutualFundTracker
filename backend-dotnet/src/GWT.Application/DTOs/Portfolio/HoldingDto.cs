namespace GWT.Application.DTOs.Portfolio;

public record HoldingDto(
    Guid Id,
    string FundId,
    decimal Units,
    decimal? AvgCost,
    DateTime PurchaseAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
