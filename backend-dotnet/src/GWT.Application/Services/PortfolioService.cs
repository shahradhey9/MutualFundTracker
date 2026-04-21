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
    private readonly IFundMetaRepository _funds;
    private readonly IYahooFinanceService _yahoo;
    private readonly ICacheService _cache;
    private readonly ILogger<PortfolioService> _logger;

    // Portfolio is assembled from stored FundMeta.LatestNav, refreshed on demand for
    // stale global holdings. NAVs are also kept fresh by NavSyncBackgroundService (daily at
    // 15:00 UTC), but on the Render free tier the process may be asleep at that time.
    private static readonly TimeSpan PortfolioCacheTtl = TimeSpan.FromMinutes(5);

    public PortfolioService(
        IHoldingRepository holdings,
        IFundMetaRepository funds,
        IYahooFinanceService yahoo,
        ICacheService cache,
        ILogger<PortfolioService> logger)
    {
        _holdings = holdings;
        _funds = funds;
        _yahoo = yahoo;
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

        // For global holdings whose stored NAV is from a previous day, fetch live prices
        // from Yahoo Finance so the portfolio always shows today's market value.
        var today = DateTime.UtcNow.Date;
        var staleGlobal = userHoldings
            .Where(h => h.Fund.Region == Region.GLOBAL &&
                        (h.Fund.NavDate is null || h.Fund.NavDate.Value.Date < today))
            .ToList();

        // liveNav overrides: ticker → live price (only populated when Yahoo call succeeds)
        var liveNav = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (staleGlobal.Count > 0)
        {
            try
            {
                var tickers = staleGlobal.Select(h => h.Fund.Ticker).Distinct();
                var quotes = await _yahoo.GetBatchQuotesAsync(tickers, ct);

                foreach (var (ticker, q) in quotes)
                    liveNav[ticker] = q.Price;

                // Persist fresh NAVs so subsequent loads don't need another Yahoo round-trip
                var tickerToFundId = staleGlobal.ToDictionary(h => h.Fund.Ticker, h => h.Fund.Id);
                var updates = quotes
                    .Where(kvp => tickerToFundId.ContainsKey(kvp.Key))
                    .Select(kvp => (
                        FundId: tickerToFundId[kvp.Key],
                        Nav:    kvp.Value.Price,
                        NavDate: kvp.Value.Timestamp))
                    .ToList();

                if (updates.Count > 0)
                    await _funds.UpdateNavBatchAsync(updates, ct);

                _logger.LogInformation(
                    "Portfolio: live NAV refresh for {Count} stale global holding(s), user {UserId}",
                    updates.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Portfolio: live NAV refresh failed — falling back to stored NAV for user {UserId}", userId);
            }
        }

        var result = userHoldings.Select(h =>
        {
            // Prefer the freshly-fetched price; fall back to what is stored in DB
            var nav = h.Fund.Region == Region.GLOBAL && liveNav.TryGetValue(h.Fund.Ticker, out var p)
                ? (decimal?)p
                : h.Fund.LatestNav;

            var currency = h.Fund.Region == Region.INDIA ? "INR" : "USD";

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
