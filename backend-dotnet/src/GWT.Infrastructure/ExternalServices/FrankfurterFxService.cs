using System.Text.Json;
using GWT.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.ExternalServices;

/// <summary>
/// Fetches USD/INR (and other) exchange rates from api.frankfurter.app.
/// Proxied through the backend so the browser never makes a cross-origin call.
/// Falls back to a hardcoded rate if the upstream API is unavailable.
/// </summary>
public class FrankfurterFxService : IFxService
{
    private readonly HttpClient _http;
    private readonly ICacheService _cache;
    private readonly ILogger<FrankfurterFxService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Hardcoded fallback rates (updated periodically)
    private static readonly Dictionary<(string, string), decimal> FallbackRates = new()
    {
        { ("USD", "INR"), 84.47m },
        { ("INR", "USD"), 1m / 84.47m },
        { ("EUR", "INR"), 91.50m },
        { ("GBP", "INR"), 107.00m },
    };

    public FrankfurterFxService(HttpClient http, ICacheService cache, ILogger<FrankfurterFxService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<FxRateDto> GetRateAsync(string from, string to, CancellationToken ct = default)
    {
        var cacheKey = $"fx:{from}:{to}";
        var cached = await _cache.GetAsync<FxRateDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var live = await TryFrankfurterAsync(from, to, ct);
        if (live is not null)
        {
            await _cache.SetAsync(cacheKey, live, CacheTtl, ct);
            return live;
        }

        _logger.LogWarning("Frankfurter unavailable for {From}/{To} — using fallback rate", from, to);
        return GetFallbackRate(from, to);
    }

    private async Task<FxRateDto?> TryFrankfurterAsync(string from, string to, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.frankfurter.app/latest?from={from}&to={to}";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                rates.TryGetProperty(to, out var rateEl))
            {
                return new FxRateDto(rateEl.GetDecimal(), from, to, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Frankfurter FX fetch failed for {From}/{To}", from, to);
        }
        return null;
    }

    private static FxRateDto GetFallbackRate(string from, string to)
    {
        if (FallbackRates.TryGetValue((from, to), out var rate))
            return new FxRateDto(rate, from, to, false);

        // If same currency, rate is 1
        if (from == to) return new FxRateDto(1m, from, to, true);

        return new FxRateDto(1m, from, to, false);
    }
}
