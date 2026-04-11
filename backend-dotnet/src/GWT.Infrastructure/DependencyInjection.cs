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
        // Render injects the connection string as a postgres:// URL.
        // Npgsql's DbConnectionStringBuilder expects key=value format, so convert if needed.
        var rawConn = configuration.GetConnectionString("DefaultConnection") ?? "";
        var connStr = ConvertPostgresUrl(rawConn);

        services.AddDbContext<GwtDbContext>(options =>
            options.UseNpgsql(
                connStr,
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

    // Render (and many PaaS providers) supply PostgreSQL as a URL:
    //   postgres://user:pass@host:5432/dbname
    // Npgsql's connection string builder requires key=value pairs, not a URI.
    // This method converts the URL format to Npgsql key=value format transparently.
    private static string ConvertPostgresUrl(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var db   = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }
}
