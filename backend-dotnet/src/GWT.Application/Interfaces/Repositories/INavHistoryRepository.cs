using GWT.Domain.Entities;

namespace GWT.Application.Interfaces.Repositories;

public interface INavHistoryRepository
{
    Task UpsertBatchAsync(IEnumerable<NavHistory> entries, CancellationToken ct = default);
    Task<List<NavHistory>> GetByFundAsync(string fundId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
