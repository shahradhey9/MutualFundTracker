using GWT.Application.DTOs.Portfolio;
using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Domain.Entities;
using GWT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GWT.Application.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IHoldingRepository _holdings;
    private readonly ICacheService _cache;
    private readonly ILogger<PortfolioService> _logger;

    // Portfolio is assembled from stored FundMeta.LatestNav — no live HTTP calls.
    // NAVs are refreshed daily by NavSyncBackgroundService and on first add by EnsureFundAsync.
    // Cache TTL of 5 min avoids redundant DB queries on repeated page loads.
    private static readonly TimeSpan PortfolioCacheTtl = TimeSpan.FromMinutes(5);

    public PortfolioService(
        IHoldingRepository holdings,
        ICacheService cache,
        ILogger<PortfolioService> logger)
    {
        _holdings = holdings;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<PortfolioItemDto>> GetPortfolioAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"portfolio:{userId}";
        var cached = await _cache.GetAsync<List<PortfolioItemDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var userHoldings = await _holdings.GetByUserAsync(userId, ct);
        if (userHoldings.Count == 0) return [];

        // Use FundMeta.LatestNav (stored in DB) instead of live HTTP calls.
        // NAVs are kept fresh by:
        //   • EnsureFundAsync — fetches NAV when the fund is first added to the catalogue.
        //   • NavSyncBackgroundService — runs SyncAllAsync daily at 15:00 UTC.
        // This makes portfolio load a single DB query with no external dependencies.
        var result = userHoldings.Select(h =>
        {
            var liveNav  = h.Fund.LatestNav;
            var currency = h.Fund.Region == Region.INDIA ? "INR" : "USD";

            decimal? currentValue = liveNav > 0 ? h.Units * liveNav : null;
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
                LiveNav:      liveNav > 0 ? liveNav : null,
                CurrentValue: currentValue,
                CostBasis:    costBasis,
                Gain:         gain,
                GainPct:      gainPct.HasValue ? Math.Round(gainPct.Value, 2) : null,
                PurchaseAt:   h.PurchaseAt,
                NavDate:      h.Fund.NavDate
            );
        }).ToList();

        await _cache.SetAsync(cacheKey, result, PortfolioCacheTtl, ct);
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

        await _cache.DeleteAsync($"portfolio:{userId}", ct);
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
        await _cache.DeleteAsync($"portfolio:{userId}", ct);
        return ToDto(updated);
    }

    public async Task DeleteHoldingAsync(Guid userId, Guid holdingId, CancellationToken ct = default)
    {
        var holding = await _holdings.GetByIdAsync(holdingId, ct)
                      ?? throw new KeyNotFoundException("Holding not found.");

        if (holding.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this holding.");

        await _holdings.DeleteAsync(holding, ct);
        await _cache.DeleteAsync($"portfolio:{userId}", ct);
        _logger.LogInformation("Holding {HoldingId} deleted for user {UserId}", holdingId, userId);
    }

    private static HoldingDto ToDto(Holding h) =>
        new(h.Id, h.FundId, h.Units, h.AvgCost, h.PurchaseAt, h.CreatedAt, h.UpdatedAt);
}
