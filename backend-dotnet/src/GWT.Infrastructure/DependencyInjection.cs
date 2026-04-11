using GWT.Application.Interfaces.Repositories;
using GWT.Application.Interfaces.Services;
using GWT.Infrastructure.Caching;
using GWT.Infrastructure.Data;
using GWT.Infrastructure.ExternalServices;
using GWT.Infrastructure.Jobs;
using GWT.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace GWT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────
        services.AddDbContext<GwtDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(GwtDbContext).Assembly.FullName)));

        // ── Redis ─────────────────────────────────────────────────────────
        // abortConnect=false in the connection string allows the app to start
        // even if Redis is unavailable; RedisCacheService degrades gracefully to DB fallback.
        var redisConn = configuration.GetConnectionString("Redis")
                        ?? "localhost:6379,abortConnect=false";
        var redisConfig = ConfigurationOptions.Parse(redisConn);
        redisConfig.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConfig));
        services.AddScoped<ICacheService, RedisCacheService>();

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFundMetaRepository, FundMetaRepository>();
        services.AddScoped<IHoldingRepository, HoldingRepository>();
        services.AddScoped<INavHistoryRepository, NavHistoryRepository>();

        // ── External HTTP Clients ─────────────────────────────────────────
        services.AddHttpClient<IAmfiService, AmfiService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "GWT/1.0 (wealth-tracker)");
        });

        services.AddHttpClient<IYahooFinanceService, YahooFinanceService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (compatible; GWT/1.0)");
        });

        // ── Background Job ────────────────────────────────────────────────
        services.AddHostedService<NavSyncBackgroundService>();

        return services;
    }
}
