namespace GWT.Application.DTOs.Goals;

public record TaggedFundDto(
    Guid HoldingId,
    string FundId,
    string Name,
    decimal? CurrentValue,
    decimal? CostBasis,
    decimal? Gain,
    decimal? GainPct
);

public record GoalDto(
    Guid Id,
    string Title,
    string GoalType,
    decimal TargetAmount,
    decimal InflationAdjustedTarget,
    DateTime EndDate,
    string Priority,
    decimal InflationRate,
    int TargetDebtPct,
    int TargetEquityPct,
    decimal CurrentAmount,
    decimal? Growth,
    decimal Progress,
    int ActualEquityPct,
    int ActualDebtPct,
    double YearsToGo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<TaggedFundDto> TaggedFunds
);
