using GWT.Application.Interfaces.Repositories;
using GWT.Domain.Entities;
using GWT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GWT.Infrastructure.Repositories;

public class HoldingRepository : IHoldingRepository
{
    private readonly GwtDbContext _db;

    public HoldingRepository(GwtDbContext db) => _db = db;

    public Task<Holding?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Holdings
            .Include(h => h.Fund)
            .FirstOrDefaultAsync(h => h.Id == id, ct);

    public Task<Holding?> GetByUserAndFundAsync(Guid userId, string fundId, CancellationToken ct = default) =>
        _db.Holdings.FirstOrDefaultAsync(h => h.UserId == userId && h.FundId == fundId, ct);

    public Task<List<Holding>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.Holdings
            .Include(h => h.Fund)
            .Where(h => h.UserId == userId)
            .ToListAsync(ct);

    public async Task<Holding> CreateAsync(Holding holding, CancellationToken ct = default)
    {
        _db.Holdings.Add(holding);
        await _db.SaveChangesAsync(ct);
        return holding;
    }

    public async Task<Holding> UpdateAsync(Holding holding, CancellationToken ct = default)
    {
        _db.Holdings.Update(holding);
        await _db.SaveChangesAsync(ct);
        return holding;
    }

    public async Task DeleteAsync(Holding holding, CancellationToken ct = default)
    {
        _db.Holdings.Remove(holding);
        await _db.SaveChangesAsync(ct);
    }
}
