namespace GWT.Application.Interfaces.Services;

public interface INavSyncService
{
    Task SyncAllAsync(CancellationToken ct = default);
}
