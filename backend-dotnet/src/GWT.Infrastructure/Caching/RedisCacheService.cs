using System.Text.Json;
using GWT.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GWT.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var value = await Db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>(value!, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key '{Key}'. Falling back to source.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOpts);
            await Db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key '{Key}'.", key);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await Db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DEL failed for key '{Key}'.", key);
        }
    }

    public async Task DeleteByPatternAsync(string pattern, CancellationToken ct = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            if (keys.Length > 0)
                await Db.KeyDeleteAsync(keys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis pattern DEL failed for pattern '{Pattern}'.", pattern);
        }
    }
}
