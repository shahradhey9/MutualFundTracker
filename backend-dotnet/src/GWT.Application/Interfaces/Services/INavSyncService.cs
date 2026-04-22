namespace GWT.Application.Interfaces.Services;

public interface INavSyncService
{
    /// <summary>Sync NAV for all India mutual funds via AMFI.</summary>
    Task SyncIndiaAsync(CancellationToken ct = default);

    /// <summary>Sync NAV for all Global funds that belong to the given IANA timezone.</summary>
    Task SyncGlobalByTimezoneAsync(string timezone, CancellationToken ct = default);

    /// <summary>Sync all funds — India + all Global. Used by the admin endpoint.</summary>
    Task SyncAllAsync(CancellationToken ct = default);
}
