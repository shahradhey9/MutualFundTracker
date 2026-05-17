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
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GWT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────
        // DATABASE_URL env var takes priority (Fly.io / Render / Heroku standard).
        // Falls back to ConnectionStrings:DefaultConnection from appsettings / env.
        // Npgsql's DbConnectionStringBuilder expects key=value format, so convert if needed.
        var rawConn = Environment.GetEnvironmentVariable("DATABASE_URL")
                      ?? configuration.GetConnectionString("DefaultConnection")
                      ?? "";
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
        // Singleton because YahooFinanceService (also singleton) depends on it,
        // and Redis operations are inherently thread-safe.
        services.AddSingleton<ICacheService, RedisCacheService>();

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFundMetaRepository, FundMetaRepository>();
        services.AddScoped<IHoldingRepository, HoldingRepository>();
        services.AddScoped<INavHistoryRepository, NavHistoryRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();

        // ── External HTTP Clients ─────────────────────────────────────────
        services.AddHttpClient<IAmfiService, AmfiService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "GWT/1.0 (wealth-tracker)");
        });

        services.AddHttpClient<INasdaqService, NasdaqService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "GWT/1.0 (wealth-tracker)");
        });

        // Yahoo Finance requires crumb-based auth — the crumb is tied to session cookies.
        // IHttpClientFactory rotates the underlying handler every 2 min by default, which
        // would lose cookies and invalidate the crumb. We therefore create a long-lived
        // HttpClient with its own CookieContainer and register the service as a singleton.
        {
            var cookieContainer = new System.Net.CookieContainer();
            var handler = new System.Net.Http.HttpClientHandler
            {
                CookieContainer  = cookieContainer,
                UseCookies       = true,
                AllowAutoRedirect = true,
            };
            var yahooClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            yahooClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            yahooClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, */*");
            yahooClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            services.AddSingleton<IYahooFinanceService>(sp => new YahooFinanceService(
                yahooClient,
                sp.GetRequiredService<ICacheService>(),
                sp.GetRequiredService<ILogger<YahooFinanceService>>()));
        }

        services.AddHttpClient<IFxService, FrankfurterFxService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "GWT/1.0");
        });

        // ── Background Job ────────────────────────────────────────────────
        services.AddHostedService<NavSyncBackgroundService>();

        return services;
    }

    // Render (and many PaaS providers) supply PostgreSQL as a URL:
    //   postgres://user:pass@host:5432/dbname
    // Npgsql's connection string builder requires key=value pairs, not a URI.
    // Use regex instead of new Uri() — .NET doesn't parse authority for unregistered schemes.
    private static string ConvertPostgresUrl(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        // Pattern: postgresql://user:pass@host:port/db?...
        var match = System.Text.RegularExpressions.Regex.Match(
            connectionString,
            @"^postgresql?://(?<user>[^:]+):(?<pass>[^@]+)@(?<host>[^:/]+)(?::(?<port>\d+))?/(?<db>[^?]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return connectionString;

        var user = Uri.UnescapeDataString(match.Groups["user"].Value);
        var pass = Uri.UnescapeDataString(match.Groups["pass"].Value);
        var host = match.Groups["host"].Value;
        var port = match.Groups["port"].Success ? int.Parse(match.Groups["port"].Value) : 5432;
        var db   = match.Groups["db"].Value;

        return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }
}
