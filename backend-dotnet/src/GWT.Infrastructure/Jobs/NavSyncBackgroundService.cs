using GWT.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GWT.Infrastructure.Jobs;

/// <summary>
/// Hosted background service that runs the NAV sync daily at 15:00 UTC
/// (equivalent to 8:30 PM IST, after AMFI publishes end-of-day NAVs).
/// </summary>
public class NavSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IYahooFinanceService _yahoo;
    private readonly ILogger<NavSyncBackgroundService> _logger;

    // Daily sync at 15:00 UTC
    private static readonly TimeOnly SyncTime = new(15, 0, 0);

    // IYahooFinanceService is a singleton — inject directly so we can warm up its crumb.
    public NavSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IYahooFinanceService yahoo,
        ILogger<NavSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _yahoo = yahoo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NAV sync background service started. Scheduled daily at {Time} UTC.", SyncTime);

        // Warm up caches at startup so the first user request is fast.
        // Both tasks run in parallel and are non-fatal if they fail.
        _ = Task.Run(async () =>
        {
            try
            {
                // Give the rest of the application a moment to finish initialising.
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                // Run AMFI and Yahoo warm-ups in parallel.
                var amfiTask = WarmUpAmfiAsync(stoppingToken);
                var yahooTask = WarmUpYahooAsync(stoppingToken);
                await Task.WhenAll(amfiTask, yahooTask);
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warm-up task encountered an error.");
            }
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelay();
            _logger.LogDebug("Next NAV sync in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NAV sync background service encountered an error.");
                // Back off for 5 minutes before retrying
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task WarmUpAmfiAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var amfi = scope.ServiceProvider.GetRequiredService<IAmfiService>();
            await amfi.FetchAllNavsAsync(ct);
            _logger.LogInformation("AMFI warm-up complete — NAVAll.txt cached in memory.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AMFI warm-up failed — first India search will be slower.");
        }
    }

    private async Task WarmUpYahooAsync(CancellationToken ct)
    {
        try
        {
            // A lightweight search is enough to trigger EnsureCrumbAsync inside the service.
            // The crumb is then cached in the singleton for all subsequent Yahoo calls.
            await _yahoo.SearchAsync("vanguard", ct);
            _logger.LogInformation("Yahoo Finance warm-up complete — crumb cached.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo Finance warm-up failed — first Global search will be slower.");
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<INavSyncService>();
        await syncService.SyncAllAsync(ct);
    }

    private static TimeSpan CalculateDelay()
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date.Add(SyncTime.ToTimeSpan());
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);
        return nextRun - now;
    }
}
