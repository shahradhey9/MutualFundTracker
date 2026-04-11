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
    private readonly IAmfiService _amfi;
    private readonly IYahooFinanceService _yahoo;
    private readonly ICacheService _cache;
    private readonly ILogger<PortfolioService> _logger;

    private static readonly TimeSpan PortfolioCacheTtl = TimeSpan.FromMinutes(1);

    public PortfolioService(
        IHoldingRepository holdings,
        IFundMetaRepository funds,
        IAmfiService amfi,
        IYahooFinanceService yahoo,
        ICacheService cache,
        ILogger<PortfolioService> logger)
    {
        _holdings = holdings;
        _funds = funds;
        _amfi = amfi;
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

        // Fetch live NAVs — split by region to use the right source
        var indiaHoldings = userHoldings.Where(h => h.Fund.Region == Region.INDIA).ToList();
        var globalHoldings = userHoldings.Where(h => h.Fund.Region == Region.GLOBAL).ToList();

        var navMap      = new Dictionary<string, decimal>();
        var currencyMap = new Dictionary<string, string>();

        // India: fetch NAVs from AMFI (single HTTP call, all in-memory)
        // Wrapped in try-catch: a transient AMFI failure must not blank the whole portfolio.
        try
        {
            foreach (var h in indiaHoldings)
            {
                currencyMap[h.FundId] = "INR";
                if (h.Fund.SchemeCode is not null)
                {
                    var nav = await _amfi.GetNavAsync(h.Fund.SchemeCode, ct);
                    if (nav.HasValue) navMap[h.FundId] = nav.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AMFI NAV fetch failed during portfolio load — holdings will show without live NAV");
            foreach (var h in indiaHoldings) currencyMap[h.FundId] = "INR";
        }

        // Global: batch fetch from Yahoo to avoid N+1
        // Also wrapped — Yahoo failure must not prevent portfolio from rendering.
        if (globalHoldings.Count > 0)
        {
            try
            {
                var tickers = globalHoldings.Select(h => h.Fund.Ticker).Distinct();
                var quotes  = await _yahoo.GetBatchQuotesAsync(tickers, ct);
                foreach (var h in globalHoldings)
                {
                    if (quotes.TryGetValue(h.Fund.Ticker, out var q))
                    {
                        navMap[h.FundId]      = q.Price;
                        currencyMap[h.FundId] = q.Currency ?? "USD";
                    }
                    else
                    {
                        currencyMap[h.FundId] = "USD";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yahoo Finance batch quote fetch failed during portfolio load — holdings will show without live NAV");
                foreach (var h in globalHoldings) currencyMap[h.FundId] = "USD";
            }
        }

        var result = userHoldings.Select(h =>
        {
            navMap.TryGetValue(h.FundId, out var liveNav);
            var currency = currencyMap.GetValueOrDefault(h.FundId,
                h.Fund.Region == Region.INDIA ? "INR" : "USD");

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
