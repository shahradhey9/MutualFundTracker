using GWT.Application.Interfaces.Repositories;
using GWT.Domain.Entities;
using GWT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GWT.Infrastructure.Repositories;

public class FundMetaRepository : IFundMetaRepository
{
    private readonly GwtDbContext _db;

    public FundMetaRepository(GwtDbContext db) => _db = db;

    public Task<FundMeta?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.FundMetas.FindAsync([id], ct).AsTask();

    public Task<FundMeta?> GetByTickerAsync(string ticker, CancellationToken ct = default) =>
        _db.FundMetas.FirstOrDefaultAsync(f => f.Ticker == ticker, ct);

    public Task<List<FundMeta>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default) =>
        _db.FundMetas.Where(f => ids.Contains(f.Id)).ToListAsync(ct);

    public Task<List<FundMeta>> GetAllHeldFundsAsync(CancellationToken ct = default) =>
        _db.FundMetas
            .Where(f => f.Holdings.Any())
            .ToListAsync(ct);

    public async Task<FundMeta> UpsertAsync(FundMeta fund, CancellationToken ct = default)
    {
        var existing = await _db.FundMetas.FindAsync([fund.Id], ct);
        if (existing is null)
        {
            _db.FundMetas.Add(fund);
        }
        else
        {
            existing.Name = fund.Name;
            existing.Amc = fund.Amc;
            existing.Ticker = fund.Ticker;
            existing.SchemeCode = fund.SchemeCode;
            existing.Category = fund.Category;
            existing.Isin = fund.Isin;
            existing.LatestNav = fund.LatestNav ?? existing.LatestNav;
            existing.NavDate = fund.NavDate ?? existing.NavDate;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? fund;
    }

    public async Task UpdateNavBatchAsync(
        IEnumerable<(string FundId, decimal Nav, DateTime NavDate)> updates,
        CancellationToken ct = default)
    {
        var ids = updates.Select(u => u.FundId).ToList();
        var funds = await _db.FundMetas.Where(f => ids.Contains(f.Id)).ToListAsync(ct);
        var updateMap = updates.ToDictionary(u => u.FundId);

        foreach (var fund in funds)
        {
            if (!updateMap.TryGetValue(fund.Id, out var upd)) continue;
            fund.LatestNav = upd.Nav;
            fund.NavDate = upd.NavDate;
            fund.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
