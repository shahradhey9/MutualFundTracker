namespace GWT.Application.DTOs.Goals;

public record CreateGoalRequestDto(
    string Title,
    string GoalType,
    decimal TargetAmount,
    DateTime EndDate,
    string Priority,
    decimal InflationRate,
    int TargetDebtPct,
    List<Guid> HoldingIds
);
