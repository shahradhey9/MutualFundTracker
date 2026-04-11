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
    private readonly ILogger<NavSyncBackgroundService> _logger;

    // Daily sync at 15:00 UTC
    private static readonly TimeOnly SyncTime = new(15, 0, 0);

    public NavSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NavSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NAV sync background service started. Scheduled daily at {Time} UTC.", SyncTime);

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
