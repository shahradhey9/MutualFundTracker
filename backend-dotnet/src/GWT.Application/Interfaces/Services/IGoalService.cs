using GWT.Application.DTOs.Goals;

namespace GWT.Application.Interfaces.Services;

public interface IGoalService
{
    Task<List<GoalDto>> GetGoalsAsync(Guid userId, CancellationToken ct = default);
    Task<GoalDto> CreateGoalAsync(Guid userId, CreateGoalRequestDto request, CancellationToken ct = default);
    Task<GoalDto> UpdateGoalAsync(Guid userId, Guid goalId, UpdateGoalRequestDto request, CancellationToken ct = default);
    Task DeleteGoalAsync(Guid userId, Guid goalId, CancellationToken ct = default);
}
