using GWT.Application.DTOs.Goals;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using GWT.Domain.Enums;

namespace GWT.Application.Services;

public class GoalService : IGoalService
{
    private readonly IGoalRepository _goals;
    private readonly IYahooFinanceService _yahoo;

    public GoalService(IGoalRepository goals, IYahooFinanceService yahoo)
    {
        _goals = goals;
        _yahoo = yahoo;
    }

    public async Task<List<GoalDto>> GetGoalsAsync(Guid userId, CancellationToken ct = default)
    {
        var goals = await _goals.GetByUserAsync(userId, ct);
        return goals.Select(ToDto).ToList();
    }

    public async Task<GoalDto> CreateGoalAsync(Guid userId, CreateGoalRequestDto request, CancellationToken ct = default)
    {
        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            GoalType = request.GoalType,
            TargetAmount = request.TargetAmount,
            EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
            Priority = request.Priority,
            InflationRate = request.InflationRate,
            TargetDebtPct = Math.Clamp(request.TargetDebtPct, 0, 100),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _goals.CreateAsync(goal, ct);
        await _goals.ReplaceGoalFundsAsync(goal.Id, request.HoldingIds, ct);

        var created = await _goals.GetByIdAsync(goal.Id, ct);
        return ToDto(created!);
    }

    public async Task<GoalDto> UpdateGoalAsync(Guid userId, Guid goalId, UpdateGoalRequestDto request, CancellationToken ct = default)
    {
        var goal = await _goals.GetByIdAsync(goalId, ct)
            ?? throw new KeyNotFoundException("Goal not found.");

        if (goal.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this goal.");

        if (request.Title is not null) goal.Title = request.Title;
        if (request.GoalType is not null) goal.GoalType = request.GoalType;
        if (request.TargetAmount.HasValue) goal.TargetAmount = request.TargetAmount.Value;
        if (request.EndDate.HasValue) goal.EndDate = DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc);
        if (request.Priority is not null) goal.Priority = request.Priority;
        if (request.InflationRate.HasValue) goal.InflationRate = request.InflationRate.Value;
        if (request.TargetDebtPct.HasValue) goal.TargetDebtPct = Math.Clamp(request.TargetDebtPct.Value, 0, 100);
        goal.UpdatedAt = DateTime.UtcNow;

        await _goals.UpdateAsync(goal, ct);

        if (request.HoldingIds is not null)
            await _goals.ReplaceGoalFundsAsync(goalId, request.HoldingIds, ct);

        var updated = await _goals.GetByIdAsync(goalId, ct);
        return ToDto(updated!);
    }

    public async Task DeleteGoalAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await _goals.GetByIdAsync(goalId, ct)
            ?? throw new KeyNotFoundException("Goal not found.");

        if (goal.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this goal.");

        await _goals.DeleteAsync(goal, ct);
    }

    private GoalDto ToDto(Goal goal)
    {
        var globalNavSnapshot = _yahoo.GetGlobalNavSnapshot();

        var taggedFunds = new List<TaggedFundDto>();
        decimal totalCurrentValue = 0;
        decimal totalCostBasis = 0;
        decimal totalEquityValue = 0;
        decimal totalDebtValue = 0;

        foreach (var gf in goal.GoalFunds)
        {
            var h = gf.Holding;
            decimal? nav;
            if (h.Fund.Region == Region.GLOBAL && globalNavSnapshot.TryGetValue(h.Fund.Ticker, out var q))
                nav = q.Price;
            else
                nav = h.Fund.LatestNav;

            decimal? currentValue = nav > 0 ? h.Units * nav : null;
            decimal? costBasis    = h.AvgCost.HasValue ? h.Units * h.AvgCost.Value : null;
            decimal? gain         = (currentValue.HasValue && costBasis.HasValue) ? currentValue - costBasis : null;
            decimal? gainPct      = (gain.HasValue && costBasis is > 0) ? gain / costBasis * 100 : null;

            taggedFunds.Add(new TaggedFundDto(
                HoldingId: h.Id,
                FundId: h.FundId,
                Name: h.Fund.Name,
                CurrentValue: currentValue,
                CostBasis: costBasis,
                Gain: gain,
                GainPct: gainPct.HasValue ? Math.Round(gainPct.Value, 2) : null
            ));

            if (currentValue.HasValue)
            {
                totalCurrentValue += currentValue.Value;
                if (IsEquityFund(h.Fund.Category)) totalEquityValue += currentValue.Value;
                else totalDebtValue += currentValue.Value;
            }
            if (costBasis.HasValue) totalCostBasis += costBasis.Value;
        }

        // Inflation-adjusted target: TargetAmount * (1 + rate/100)^years
        var yearsToGo = Math.Max(0, (goal.EndDate - DateTime.UtcNow).TotalDays / 365.25);
        var inflationAdjustedTarget = goal.TargetAmount * (decimal)Math.Pow(1 + (double)goal.InflationRate / 100, yearsToGo);

        var progress = inflationAdjustedTarget > 0
            ? Math.Round(totalCurrentValue / inflationAdjustedTarget * 100, 2)
            : 0;

        decimal? growth = null;
        if (totalCostBasis > 0)
            growth = Math.Round((totalCurrentValue - totalCostBasis) / totalCostBasis * 100, 2);

        int actualEquityPct = 0;
        int actualDebtPct = 0;
        if (totalCurrentValue > 0)
        {
            actualEquityPct = (int)Math.Round(totalEquityValue / totalCurrentValue * 100);
            actualDebtPct   = 100 - actualEquityPct;
        }

        return new GoalDto(
            Id: goal.Id,
            Title: goal.Title,
            GoalType: goal.GoalType,
            TargetAmount: goal.TargetAmount,
            InflationAdjustedTarget: Math.Round(inflationAdjustedTarget, 0),
            EndDate: goal.EndDate,
            Priority: goal.Priority,
            InflationRate: goal.InflationRate,
            TargetDebtPct: goal.TargetDebtPct,
            TargetEquityPct: 100 - goal.TargetDebtPct,
            CurrentAmount: totalCurrentValue,
            Growth: growth,
            Progress: progress,
            ActualEquityPct: actualEquityPct,
            ActualDebtPct: actualDebtPct,
            YearsToGo: Math.Round(yearsToGo, 2),
            CreatedAt: goal.CreatedAt,
            UpdatedAt: goal.UpdatedAt,
            TaggedFunds: taggedFunds
        );
    }

    private static bool IsEquityFund(string? category)
    {
        if (string.IsNullOrEmpty(category)) return true;
        var lower = category.ToLowerInvariant();
        // Debt keyword check — everything else is treated as equity
        return !(lower.Contains("debt") || lower.Contains("bond") || lower.Contains("liquid") ||
                 lower.Contains("money market") || lower.Contains("ultra short") ||
                 lower.Contains("low duration") || lower.Contains("short duration") ||
                 lower.Contains("medium duration") || lower.Contains("dynamic bond") ||
                 lower.Contains("gilt") || lower.Contains("credit risk") ||
                 lower.Contains("overnight") || lower.Contains("banking and psu"));
    }
}
