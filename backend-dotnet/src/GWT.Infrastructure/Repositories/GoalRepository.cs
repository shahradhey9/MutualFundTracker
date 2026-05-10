using GWT.Application.Interfaces.Repositories;
using GWT.Domain.Entities;
using GWT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GWT.Infrastructure.Repositories;

public class GoalRepository : IGoalRepository
{
    private readonly GwtDbContext _db;

    public GoalRepository(GwtDbContext db) => _db = db;

    public Task<List<Goal>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.Goals
            .Include(g => g.GoalFunds)
                .ThenInclude(gf => gf.Holding)
                    .ThenInclude(h => h.Fund)
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.EndDate)
            .ToListAsync(ct);

    public Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Goals
            .Include(g => g.GoalFunds)
                .ThenInclude(gf => gf.Holding)
                    .ThenInclude(h => h.Fund)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<Goal> CreateAsync(Goal goal, CancellationToken ct = default)
    {
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task<Goal> UpdateAsync(Goal goal, CancellationToken ct = default)
    {
        _db.Goals.Update(goal);
        await _db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task DeleteAsync(Goal goal, CancellationToken ct = default)
    {
        _db.Goals.Remove(goal);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplaceGoalFundsAsync(Guid goalId, List<Guid> holdingIds, CancellationToken ct = default)
    {
        var existing = await _db.GoalFunds.Where(gf => gf.GoalId == goalId).ToListAsync(ct);
        _db.GoalFunds.RemoveRange(existing);

        var newEntries = holdingIds.Select(hid => new GoalFund
        {
            Id = Guid.NewGuid(),
            GoalId = goalId,
            HoldingId = hid,
        });
        _db.GoalFunds.AddRange(newEntries);
        await _db.SaveChangesAsync(ct);
    }
}
