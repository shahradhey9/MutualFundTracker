using GWT.Application.Interfaces.Repositories;
using GWT.Domain.Entities;
using GWT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GWT.Infrastructure.Repositories;

public class NavHistoryRepository : INavHistoryRepository
{
    private readonly GwtDbContext _db;

    public NavHistoryRepository(GwtDbContext db) => _db = db;

    public async Task UpsertBatchAsync(IEnumerable<NavHistory> entries, CancellationToken ct = default)
    {
        foreach (var entry in entries)
        {
            var existing = await _db.NavHistories
                .FirstOrDefaultAsync(n => n.FundId == entry.FundId && n.NavDate == entry.NavDate, ct);

            if (existing is null)
                _db.NavHistories.Add(entry);
            // If existing, we don't overwrite (NAV history is immutable per day)
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<List<NavHistory>> GetByFundAsync(string fundId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        _db.NavHistories
            .Where(n => n.FundId == fundId && n.NavDate >= from && n.NavDate <= to)
            .OrderBy(n => n.NavDate)
            .ToListAsync(ct);
}
