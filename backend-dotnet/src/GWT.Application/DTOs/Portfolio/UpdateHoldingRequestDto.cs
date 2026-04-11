namespace GWT.Application.DTOs.Portfolio;

public record UpdateHoldingRequestDto(
    decimal? Units,
    decimal? AvgCost,
    DateTime? PurchaseAt
);
