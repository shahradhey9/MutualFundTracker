using GWT.Application.Interfaces.Repositories;
using GWT.Domain.Entities;
using GWT.Domain.Enums;
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

    public Task<List<FundMeta>> GetAllByRegionAsync(Region region, CancellationToken ct = default) =>
        _db.FundMetas.Where(f => f.Region == region).ToListAsync(ct);

    public Task<List<FundMeta>> GetGlobalByTimezoneAsync(string timezone, CancellationToken ct = default) =>
        _db.FundMetas.Where(f => f.Region == Region.GLOBAL && f.Timezone == timezone).ToListAsync(ct);

    /// <summary>
    /// Word-by-word ILIKE search: every word in the query must appear in name, AMC, ticker, or scheme code.
    /// Matches the same logic as the AMFI in-memory search so results are consistent.
    /// </summary>
    public async Task<List<FundMeta>> SearchAsync(
        string query, Region region, int limit = 50, CancellationToken ct = default)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return [];

        var q = _db.FundMetas.Where(f => f.Region == region);
        foreach (var word in words)
        {
            var pattern = $"%{word}%";
            q = q.Where(f =>
                EF.Functions.ILike(f.Name, pattern) ||
                EF.Functions.ILike(f.Amc, pattern) ||
                EF.Functions.ILike(f.Ticker, pattern) ||
                (f.SchemeCode != null && EF.Functions.ILike(f.SchemeCode, pattern)));
        }

        return await q.Take(limit).ToListAsync(ct);
    }

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

    /// <summary>
    /// Inserts funds that do not yet exist in fund_meta and updates NAV/NavDate for those that do.
    /// Processed in chunks to stay within SQL parameter limits.
    /// </summary>
    public async Task BulkUpsertFundsAsync(IEnumerable<FundMeta> funds, CancellationToken ct = default)
    {
        var fundList = funds.ToList();
        if (fundList.Count == 0) return;

        // Discover which IDs already exist — chunk to stay well under PostgreSQL's 65535 param limit
        const int idChunk = 2000;
        var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fundList.Count; i += idChunk)
        {
            var chunkIds = fundList.Skip(i).Take(idChunk).Select(f => f.Id).ToList();
            var found = await _db.FundMetas
                .Where(f => chunkIds.Contains(f.Id))
                .Select(f => f.Id)
                .ToListAsync(ct);
            foreach (var id in found) existingIds.Add(id);
        }

        var toInsert = fundList.Where(f => !existingIds.Contains(f.Id)).ToList();
        var toUpdate = fundList
            .Where(f => existingIds.Contains(f.Id) && f.LatestNav.HasValue && f.NavDate.HasValue)
            .Select(f => (f.Id, f.LatestNav!.Value, f.NavDate!.Value))
            .ToList();

        // Insert new funds in batches so EF Core's change tracker doesn't blow up
        const int insertChunk = 500;
        for (int i = 0; i < toInsert.Count; i += insertChunk)
        {
            _db.FundMetas.AddRange(toInsert.Skip(i).Take(insertChunk));
            await _db.SaveChangesAsync(ct);
        }

        // Update NAVs for existing funds using the existing batch-update logic
        if (toUpdate.Count > 0)
            await UpdateNavBatchAsync(toUpdate, ct);
    }
}
