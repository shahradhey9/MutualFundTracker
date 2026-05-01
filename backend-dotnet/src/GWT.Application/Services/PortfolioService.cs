using System.Collections.Concurrent;
using GWT.Application.DTOs.Portfolio;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

// CurrencyHelper is in the GWT.Application namespace — no extra using needed

namespace GWT.Application.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IHoldingRepository _holdings;
    private readonly IYahooFinanceService _yahoo;
    private readonly ILogger<PortfolioService> _logger;

    // Process-level in-memory cache keyed by userId.
    // Redis is unavailable on Render free tier so we cache in process memory.
    // Static so it survives across scoped DI instances of PortfolioService.
    private static readonly ConcurrentDictionary<Guid, (List<PortfolioItemDto> Data, DateTime Expiry)> _memCache = new();
    private static readonly TimeSpan PortfolioMemCacheTtl = TimeSpan.FromMinutes(5);

    public PortfolioService(
        IHoldingRepository holdings,
        IYahooFinanceService yahoo,
        ILogger<PortfolioService> logger)
    {
        _holdings = holdings;
        _yahoo    = yahoo;
        _logger   = logger;
    }

    public async Task<List<PortfolioItemDto>> GetPortfolioAsync(Guid userId, CancellationToken ct = default)
    {
        // 1. Process-level in-memory cache (fast path — no DB, no HTTP)
        if (_memCache.TryGetValue(userId, out var entry) && DateTime.UtcNow < entry.Expiry)
            return entry.Data;

        var userHoldings = await _holdings.GetByUserAsync(userId, ct);
        if (userHoldings.Count == 0) return [];

        // 2. Read global NAVs from the in-memory cache maintained by the background service.
        //    This is refreshed every 4 hours from Yahoo Finance — zero HTTP calls on the
        //    portfolio hot path.  Falls back to the stored DB value for any ticker not yet cached.
        var globalNavSnapshot = _yahoo.GetGlobalNavSnapshot();

        var result = userHoldings.Select(h =>
        {
            decimal? nav;
            if (h.Fund.Region == Region.GLOBAL && globalNavSnapshot.TryGetValue(h.Fund.Ticker, out var q))
                nav = q.Price;
            else
                nav = h.Fund.LatestNav;

            var currency = CurrencyHelper.GetCurrency(h.Fund.Ticker, h.Fund.Timezone);

            decimal? currentValue = nav > 0 ? h.Units * nav : null;
            decimal? costBasis    = h.AvgCost.HasValue ? h.Units * h.AvgCost.Value : null;
            decimal? gain         = (currentValue.HasValue && costBasis.HasValue) ? currentValue - costBasis : null;
            decimal? gainPct      = (gain.HasValue && costBasis is > 0) ? gain / costBasis * 100 : null;

            return new PortfolioItemDto(
                HoldingId:    h.Id,
                FundId:       h.FundId,
                Name:         h.Fund.Name,
                Amc:          h.Fund.Amc,
                Ticker:       h.Fund.Ticker,
                Category:     h.Fund.Category,
                Region:       h.Fund.Region,
                Currency:     currency,
                Units:        h.Units,
                AvgCost:      h.AvgCost,
                LiveNav:      nav > 0 ? nav : null,
                CurrentValue: currentValue,
                CostBasis:    costBasis,
                Gain:         gain,
                GainPct:      gainPct.HasValue ? Math.Round(gainPct.Value, 2) : null,
                PurchaseAt:   h.PurchaseAt,
                NavDate:      h.Fund.NavDate
            );
        }).ToList();

        // 3. Store in process-level memory cache
        _memCache[userId] = (result, DateTime.UtcNow.Add(PortfolioMemCacheTtl));
        return result;
    }

    public async Task<HoldingDto> UpsertHoldingAsync(Guid userId, AddHoldingRequestDto request, CancellationToken ct = default)
    {
        var existing = await _holdings.GetByUserAndFundAsync(userId, request.FundId, ct);

        Holding holding;
        if (existing is not null)
        {
            // Weighted-average consolidation
            var totalUnits = existing.Units + request.Units;
            decimal? newAvgCost = null;
            if (existing.AvgCost.HasValue && request.AvgCost.HasValue)
                newAvgCost = (existing.Units * existing.AvgCost.Value + request.Units * request.AvgCost.Value) / totalUnits;
            else
                newAvgCost = existing.AvgCost ?? request.AvgCost;

            existing.Units = totalUnits;
            existing.AvgCost = newAvgCost;
            // Keep the earliest purchase date
            var purchaseAtUtc = DateTime.SpecifyKind(request.PurchaseAt, DateTimeKind.Utc);
            existing.PurchaseAt = purchaseAtUtc < existing.PurchaseAt ? purchaseAtUtc : existing.PurchaseAt;
            existing.UpdatedAt = DateTime.UtcNow;
            holding = await _holdings.UpdateAsync(existing, ct);
        }
        else
        {
            holding = await _holdings.CreateAsync(new Holding
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FundId = request.FundId,
                Units = request.Units,
                AvgCost = request.AvgCost,
                PurchaseAt = DateTime.SpecifyKind(request.PurchaseAt, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, ct);
        }

        _memCache.TryRemove(userId, out _);
        _logger.LogInformation("Holding upserted for user {UserId}, fund {FundId}", userId, request.FundId);
        return ToDto(holding);
    }

    public async Task<HoldingDto> UpdateHoldingAsync(Guid userId, Guid holdingId, UpdateHoldingRequestDto request, CancellationToken ct = default)
    {
        var holding = await _holdings.GetByIdAsync(holdingId, ct)
                      ?? throw new KeyNotFoundException("Holding not found.");

        if (holding.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this holding.");

        if (request.Units.HasValue) holding.Units = request.Units.Value;
        if (request.AvgCost.HasValue) holding.AvgCost = request.AvgCost;
        if (request.PurchaseAt.HasValue) holding.PurchaseAt = DateTime.SpecifyKind(request.PurchaseAt.Value, DateTimeKind.Utc);
        holding.UpdatedAt = DateTime.UtcNow;

        var updated = await _holdings.UpdateAsync(holding, ct);
        _memCache.TryRemove(userId, out _);
        return ToDto(updated);
    }

    public async Task DeleteHoldingAsync(Guid userId, Guid holdingId, CancellationToken ct = default)
    {
        var holding = await _holdings.GetByIdAsync(holdingId, ct)
                      ?? throw new KeyNotFoundException("Holding not found.");

        if (holding.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this holding.");

        await _holdings.DeleteAsync(holding, ct);
        _memCache.TryRemove(userId, out _);
        _logger.LogInformation("Holding {HoldingId} deleted for user {UserId}", holdingId, userId);
    }

    public void InvalidateUserCache(Guid userId) => _memCache.TryRemove(userId, out _);

    public void InvalidateAllCaches() => _memCache.Clear();

    private static HoldingDto ToDto(Holding h) =>
        new(h.Id, h.FundId, h.Units, h.AvgCost, h.PurchaseAt, h.CreatedAt, h.UpdatedAt);
}
