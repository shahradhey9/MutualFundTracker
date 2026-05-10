using GWT.Domain.Entities;

namespace GWT.Application.Interfaces.Repositories;

public interface IGoalRepository
{
    Task<List<Goal>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Goal> CreateAsync(Goal goal, CancellationToken ct = default);
    Task<Goal> UpdateAsync(Goal goal, CancellationToken ct = default);
    Task DeleteAsync(Goal goal, CancellationToken ct = default);
    Task ReplaceGoalFundsAsync(Guid goalId, List<Guid> holdingIds, CancellationToken ct = default);
}
